# Service Bus sample — `migrating-azure-servicebus`

An order-processing console app: orders go onto a queue, shipments fan out through a topic
with filtered subscriptions, and the entity topology is created at startup. It is stuck on
the retired `WindowsAzure.ServiceBus` package.

- **TFM:** `net472` — `Microsoft.ServiceBus.dll` only ships a `net462` asset, so this app
  *cannot* run on .NET Core until it is migrated. That constraint is part of the test.
- **Deprecated package:** `WindowsAzure.ServiceBus`
- **Baseline:** `dotnet build` succeeds with 0 warnings, 0 errors.

## What each file exercises

| File | Legacy surface under test |
|------|---------------------------|
| `MessagingFactoryProvider.cs` | `MessagingFactory.CreateFromConnectionString`, `MessagingFactory.Create(Uri, MessagingFactorySettings)`, `ServiceBusEnvironment.CreateServiceUri`, `TokenProvider.CreateSharedAccessSignatureTokenProvider`, `TransportType.Amqp`, `CreateQueueClient` / `CreateTopicClient` / `CreateSubscriptionClient`, `ReceiveMode` |
| `OrderSender.cs` | `BrokeredMessage`, sync `Send`, `SendAsync`, `SendBatchAsync`, `Properties[...]`, `ScheduledEnqueueTimeUtc` (`DateTime`), `SessionId`, `TimeToLive`, `Label`, `ContentType`, `CorrelationId` |
| `OrderProcessor.cs` | `OnMessage`, `OnMessageAsync`, `OnMessageOptions` (`AutoComplete`, `MaxConcurrentCalls`, `AutoRenewTimeout`, `ExceptionReceived`), `GetBody<T>()`, `Complete` / `CompleteAsync` / `Abandon` / `AbandonAsync` / `DeadLetter`, `DeliveryCount`, `ReceiveBatchAsync`, `PeekAsync` |
| `ShipmentPipeline.cs` | `TopicClient`, `SubscriptionClient`, `DeadLetterAsync` with reason + description |
| `TopologyManager.cs` | `NamespaceManager`, `QueueDescription`, `TopicDescription`, `SubscriptionDescription`, `SqlFilter`, `MessageCountDetails` |
| `OrderMessage.cs` | `[DataContract]` body serialized by `BrokeredMessage`'s `DataContractSerializer` |

## Expected migration outcome

### Project and packages

- [ ] `WindowsAzure.ServiceBus` is removed; `Azure.Messaging.ServiceBus` is added.
- [ ] `Azure.Messaging.ServiceBus.Administration` types come from the **same** `Azure.Messaging.ServiceBus` package — a separate `PackageReference` for it does not exist and adding one breaks restore. (The skill's own table implies a separate package; flag it if the agent adds one.)
- [ ] Retargeting to `net8.0` is now possible and is a reasonable bonus, but is **not** required. If the agent retargets, `AutoGenerateBindingRedirects` must go too.

### Client shape

- [ ] `MessagingFactory` disappears entirely; a single `ServiceBusClient` replaces it.
- [ ] `FromSharedAccessSignature` becomes `new ServiceBusClient(fullyQualifiedNamespace, new AzureNamedKeyCredential(keyName, key))` or an equivalent — silently collapsing it into the plain connection-string overload loses the SAS path.
- [ ] `MessagingFactorySettings.OperationTimeout` maps onto `ServiceBusClientOptions.RetryOptions.TryTimeout`; `TransportType.Amqp` maps onto `ServiceBusTransportType.AmqpTcp`.
- [ ] `Close()` calls become `await DisposeAsync()`, and `IDisposable` becomes `IAsyncDisposable`.

### Sending

- [ ] `BrokeredMessage` → `ServiceBusMessage`; `Properties` → `ApplicationProperties`.
- [ ] `ScheduledEnqueueTimeUtc` (`DateTime`) → `ScheduledEnqueueTime` (`DateTimeOffset`). A migration that leaves a `DateTime` here will not compile; one that uses `DateTime.UtcNow` implicitly converted without thought is worth a second look.
- [ ] The sync `Send` becomes `await sender.SendMessageAsync(...)` and the method signature changes to return `Task`. Callers in `Program.cs` must be updated to match.
- [ ] `SendBatchAsync` with the hand-rolled 100-message chunking should become either `SendMessagesAsync(IEnumerable<ServiceBusMessage>)` or, better, a `ServiceBusMessageBatch` built with `TryAddMessage` — which is what actually fixes the "batch too large" bug the manual chunking was papering over.
- [ ] `new BrokeredMessage(order)` (DataContract-serialized) becomes an explicit serialization decision. `new ServiceBusMessage(BinaryData.FromObjectAsJson(order))` changes the wire format; the migration must either do that deliberately or preserve `DataContractSerializer`. **An unremarked wire-format change is the highest-value failure to catch in this sample.**

### Receiving

- [ ] `OnMessage` / `OnMessageAsync` become `ServiceBusProcessor` with `ProcessMessageAsync` and `ProcessErrorAsync` handlers.
- [ ] `ProcessErrorAsync` is assigned in **both** `Start()` and `StartAsync()` and in `ShipmentSubscriber.Start()`. It is mandatory — omitting it throws at `StartProcessingAsync()`. Three pumps, three error handlers.
- [ ] `OnMessageOptions.AutoComplete` → `ServiceBusProcessorOptions.AutoCompleteMessages`; `MaxConcurrentCalls` → `MaxConcurrentCalls`; `AutoRenewTimeout` → `MaxAutoLockRenewalDuration`.
- [ ] `message.Complete()` → `await args.CompleteMessageAsync(args.Message)`, and likewise for abandon and dead-letter.
- [ ] `message.GetBody<OrderMessage>()` → `args.Message.Body.ToObjectFromJson<OrderMessage>()` (consistent with whatever the sender now does).
- [ ] `DeliveryCount` still exists on `ServiceBusReceivedMessage` — the retry-budget branch must survive.
- [ ] `ReceiveBatchAsync(n, timeout)` → `ReceiveMessagesAsync(n, timeout)` on a `ServiceBusReceiver`.
- [ ] `PeekAsync()` → `PeekMessageAsync()`.
- [ ] `StartProcessingAsync()` is actually called. A processor that is configured but never started is a silent no-op.

### Administration

- [ ] `NamespaceManager` → `ServiceBusAdministrationClient`.
- [ ] `QueueDescription` → `CreateQueueOptions`; `TopicDescription` → `CreateTopicOptions`; `SubscriptionDescription` → `CreateSubscriptionOptions`.
- [ ] `MaxSizeInMegabytes` → `MaxSizeInMegabytes` (`long`); `EnablePartitioning` → `EnablePartitioning`; `RequiresDuplicateDetection` → `RequiresDuplicateDetection`.
- [ ] `CreateSubscriptionAsync(description, new SqlFilter(...))` → `CreateSubscriptionAsync(options, new CreateRuleOptions { Filter = new SqlRuleFilter(...) })`.
- [ ] `GetQueueAsync(...).MessageCountDetails.ActiveMessageCount` → `GetQueueRuntimePropertiesAsync(...)` → `.ActiveMessageCount`. Using `GetQueueAsync` here will not compile, because runtime counts moved off the description type.

### Build

- [ ] `dotnet build` succeeds.
- [ ] `git grep -n "Microsoft.ServiceBus\|BrokeredMessage\|MessagingFactory\|NamespaceManager\|OnMessage"` returns nothing.

## Running

```powershell
dotnet build
$env:CONTOSO_SERVICEBUS_CONNECTION = "Endpoint=sb://..."   # optional
dotnet run
```

Without `CONTOSO_SERVICEBUS_CONNECTION` the app prints a notice and exits without touching
the network.

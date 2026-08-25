# Progress Details

## Status
Completed.

## Research
- Confirmed `Contoso.Ordering.csproj` is an SDK-style executable targeting only `net472`, with project-local package references to `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1.
- Inventoried all queue and topic send paths, the modern JSON `OrderMessageContract`, the legacy inbound clients, and the application ownership/disposal flow.
- Consulted the official Azure Service Bus migration guide for sender construction, safe batching, scheduling, and connection ownership patterns.
- Confirmed the assessment's only issue in this subtask is `TimeSpan.FromHours(double)` in `OrderSender.cs`; the modern package and dependencies are compatible.
- Confirmed no project references, test projects, or `// STUB:` markers apply.
- Recorded the complete findings and validation plan in `task.md` before editing source.

## Changes
- Migrated `OrderSender` from legacy `QueueClient`/`BrokeredMessage` APIs to `ServiceBusSender`/`ServiceBusMessage`.
- Replaced the synchronous metadata-rich send with `SendWithMetadataAsync` and retained the existing asynchronous send paths.
- Routed every order body through `OrderMessageContract`, preserving message IDs, correlation ID, subjects, JSON content type, TTL, session ID, and existing application properties.
- Replaced fixed-count batching with negotiated `ServiceBusMessageBatch` instances, `TryAddMessage`, full-batch sends, and explicit oversized-message failure.
- Migrated scheduled delivery to `ScheduleMessageAsync`, interpreting the existing `enqueueAtUtc` ticks as UTC when creating the required `DateTimeOffset`.
- Migrated `ShipmentPublisher` to a modern topic `ServiceBusSender` while leaving `ShipmentSubscriber` unchanged on the legacy inbound API.
- Updated `Program` to own one modern `ServiceBusClient` and its queue/topic senders with asynchronous disposal, while retaining `MessagingFactoryProvider` only for the legacy queue and subscription receivers.
- Kept both Service Bus packages and the `net472` target unchanged for the staged mixed-SDK composition.

## Files Modified
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.03-outbound-messaging\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.03-outbound-messaging\progress-details.md`
- `servicebus-sample\OrderSender.cs`
- `servicebus-sample\Program.cs`
- `servicebus-sample\ShipmentPipeline.cs`

## Validation
Executed:

```text
dotnet restore .\servicebus-sample\Contoso.Ordering.csproj --verbosity minimal
dotnet build .\servicebus-sample\Contoso.Ordering.csproj --framework net472 --no-restore --configuration Release --verbosity minimal
```

Results:
- Restore succeeded.
- The complete Release build for `net472` succeeded with **0 warnings and 0 errors**.
- Output exists at `servicebus-sample\bin\Release\net472\Contoso.Ordering.exe`.
- `OrderSender` contains no legacy Service Bus namespace or client/message types.
- `ShipmentPublisher` contains no legacy client/message usage; the legacy namespace in `ShipmentPipeline.cs` remains solely for the intentionally deferred `ShipmentSubscriber`.
- `Program` creates outbound senders only from the modern client and creates inbound clients only from the legacy provider.
- `git diff --check` passed.

## Deferred Runtime Verification
No live queue/topic sends were attempted because an Azure Service Bus namespace credential is not part of this task. Live verification of message metadata, scheduled delivery, batching at the namespace limit, and interoperability with legacy inbound deserialization remains dependent on a configured Service Bus environment.

# Progress details

## 2026-09-08 — .NET 10 and Azure.Messaging.ServiceBus migration

### Work completed

- Retargeted `servicebus-sample/Contoso.Ordering.csproj` from `net472` to `net10.0`, removed .NET Framework binding-redirect settings and warning suppressions, enabled warnings-as-errors, and replaced `WindowsAzure.ServiceBus` 6.2.1 with the latest stable compatible `Azure.Messaging.ServiceBus` 7.20.2.
- Replaced the process-wide legacy factory with `ServiceBusClientProvider` and one long-lived `ServiceBusClient`. Preserved connection-string construction and explicit namespace/key-name/shared-key construction with `AzureNamedKeyCredential`. Both paths now explicitly use AMQP/TCP with exponential retry, 10 retries, a 30-second maximum retry delay, and a 60-second per-attempt `TryTimeout`; linked cancellation-token budgets preserve the legacy 60-second overall deadline across each logical operation.
- Preserved the binary XML `DataContractSerializer` message body. `OrderMessage.Serialize` and `Deserialize` use a binary XML writer/reader, a 1 MiB pre-allocation body limit, finite XML quotas, and a 65,536-item object-graph limit. A credential-free binary XML round-trip probe passed (184-byte payload with all tested values retained).
- Migrated send, batch, schedule, session, processor, pull receive, peek, settlement, and shipment paths. Processors use peek-lock and `AutoCompleteMessages=false`; each pump registers both message and error callbacks before starting. Delivery-count retry behavior and explicit complete/abandon/dead-letter paths remain.
- Replaced fixed 100-message chunks with size-aware `ServiceBusMessageBatch.TryAddMessage` batches. Scheduled UTC `DateTime` values are explicitly interpreted as UTC and passed to `ScheduleMessageAsync`.
- Migrated topology operations to the data-plane `ServiceBusAdministrationClient` in the same package, preserving queue/topic/subscription settings, SQL filters, runtime queue-depth lookup, and the explicit delete capability. No topology operation was invoked during validation.
- Updated `servicebus-sample/README.md` for .NET 10 and the Track 2 API.

### Track 2 behavior audit — Azure Service Bus

| Package or type | Shape | Outcome | Evidence | Action taken |
|---|---|---|---|---|
| `Azure.Messaging.ServiceBus` message bodies | External contract | Preserved | `OrderMessage.Serialize`/`Deserialize` retain the existing `DataContract` name, namespace, member order, and binary XML representation; local round-trip passed. No JSON conversion was introduced. | Added bounded binary XML serialization/deserialization with explicit quotas and body/object-graph caps. Live backlog interoperability remains unexercised because validation intentionally used no Azure credentials. |
| Senders, receivers, and processors | Operation contract | Changed deliberately | `OrderSender` retains IDs, subject/legacy label meaning, properties, TTL, sessions, and scheduling; `OrderProcessor` and `ShipmentSubscriber` explicitly retain peek-lock/manual settlement, concurrency, lock renewal, retry threshold, dead-letter reasons, pull timeout, and peek. | Fixed-count batching changed to size-aware batches, so network-call grouping now follows negotiated batch size instead of 100 messages; this avoids oversized batches at the cost of variable batch cardinality. Sync `Send` and pump start/stop entry points now return awaitable tasks. |
| Client/wrapper and topology surface | Boundary and topology | Changed deliberately | Before: `MessagingFactoryProvider` exposed `Factory`, two `CreateQueueClient` overloads, `CreateTopicClient`, `CreateSubscriptionClient`, two auth factories, and `Dispose`; sender/processor wrappers exposed synchronous `Close`/`Stop`. After: `ServiceBusClientProvider` exposes `Client`, `CreateSender`, both auth factories, and `DisposeAsync`; receiver/processor creation is bound explicitly to queue/topic/subscription names in `OrderProcessor` and `ShipmentSubscriber`; topology methods remain `EnsureTopologyAsync`, `GetQueueDepthAsync`, and `DeleteTopologyAsync`. | Removed the obsolete factory/client graph while retaining each capability in the Track 2 resource-bound client graph. Administration remains data-plane and no ARM package was added. |
| Authentication, transport, retries, timeout scope, and lifetime | Operational | Changed deliberately | Both authentication paths are preserved. The Track 1 connection-string path used the NetMessaging/SBMP default, while Track 2 uses explicit `AmqpTcp`; the explicit SAS-key path already selected AMQP. Messaging firewall requirements move from SBMP TCP 9350-9354 to AMQP TCP 5671-5672. Topology administration continues over HTTPS 443; AMQP-over-WebSockets/443 is not selected. Both clients now use exponential retry with 10 retries and a 30-second maximum delay. `TryTimeout` is correctly treated as per-attempt, while linked cancellation tokens enforce one 60-second budget across each complete send, batch, schedule, receive, settlement, processor lifecycle, and topology operation. | Configured identical Track 2 client options for connection-string and named-key authentication, applied the same retry budget to administration calls, propagated cancellation tokens through all scoped calls, and retained async resource disposal with one shared AMQP connection. Live authorization and network behavior remain unchecked by design. |

### Public API migration notes

- `MessagingFactoryProvider` was renamed to `ServiceBusClientProvider`; `Factory` became `Client`; entity-specific legacy factory methods were replaced by `CreateSender` plus explicit processor/receiver construction from the shared client; `Dispose` became `DisposeAsync`.
- `OrderSender.Send` now returns `Task`; `Close` became `DisposeAsync`. `SendAsync`, `SendBatchAsync`, `ScheduleAsync`, and `SendSessionAsync` remain available.
- `OrderProcessor.Start` and `StartAsync` now return `Task`; `Stop` became `StopAsync`; `DisposeAsync` was added. `DrainAsync` and `PeekAsync` remain.
- `ShipmentPublisher.Close` became `DisposeAsync`. `ShipmentSubscriber.Start`/`Stop` became `StartAsync`/`StopAsync`, and `DisposeAsync` was added.
- `TopologyManager` retained all public constants and methods. `OrderMessage` retained its data members and added explicit `Serialize`/`Deserialize` helpers.

### Validation

- `.NET 10 SDK`: compatible SDK found (`10.0.400-preview.0.26356.102` emitted informational `NETSDK1057`; this was not a build warning).
- `dotnet restore servicebus-sample/Contoso.Ordering.csproj`: passed.
- `dotnet build servicebus-sample/Contoso.Ordering.csproj --configuration Release`: passed, **0 errors / 0 warnings**; output exists at `bin/Release/net10.0/Contoso.Ordering.dll`.
- Existing affected tests: none found in the repository.
- Credential-free `dotnet run --no-build`: passed and exited before contacting Azure.
- Scoped legacy search for `WindowsAzure.ServiceBus`, `Microsoft.ServiceBus`, `BrokeredMessage`, `MessagingFactory`, `NamespaceManager`, and legacy `OnMessage` symbols: no matches.
- `git diff --check`: passed. No sibling project or infrastructure file was modified.
- Code review found one pre-allocation body-size issue; it was fixed by checking `BinaryData.ToMemory().Length` before copying.

### Deviations and remaining user verification

- No live Service Bus operation was run, so deployed namespace permissions, existing backlog interoperability, and broker-side topology behavior remain intentionally unverified.
- `get_project_dependencies` could not be used because the repository contains no `.sln`/`.slnx`; direct project inspection and `dotnet list package --include-transitive` confirmed project-local package management and the `Azure.Core` dependency.

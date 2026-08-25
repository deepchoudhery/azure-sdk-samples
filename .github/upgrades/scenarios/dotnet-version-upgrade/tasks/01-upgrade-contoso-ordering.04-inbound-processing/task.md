# 01-upgrade-contoso-ordering.04-inbound-processing: Migrate queue and subscription processing to Service Bus processors and receivers

## Objective
Migrate all inbound queue and subscription behavior to modern Service Bus processors/receivers and complete the application-level transition to a single modern `ServiceBusClient`.

## Scope
- `servicebus-sample/OrderProcessor.cs`
- `ShipmentSubscriber` code in `servicebus-sample/ShipmentPipeline.cs`
- `servicebus-sample/Program.cs`
- The shared message serialization helper created by task 01 only if a narrowly scoped correction is required

## Steps
1. Replace queue `OnMessage`/`OnMessageAsync` pumps with `ServiceBusProcessor` handlers configured for manual completion, the existing concurrency levels, lock renewal, and PeekLock behavior.
2. Map complete, abandon, dead-letter, delivery count, application property access, and error reporting to `ProcessMessageEventArgs` and `ProcessErrorEventArgs`. Preserve poison-message and retry-budget behavior.
3. Replace pull-based batch receive and peek operations with `ServiceBusReceiver` APIs, using the shared `OrderMessage` deserializer and explicitly settling drained messages.
4. Replace subscription processing in `ShipmentSubscriber` with a modern processor, preserving carrier property handling and dead-letter behavior.
5. Convert start/stop lifecycle methods to async where required and update `Program.cs` so all senders, receivers, and processors originate from the modern `ServiceBusClient`; remove application runtime use of `MessagingFactoryProvider`.
6. Resolve assessment-reported `TimeSpan.FromMinutes` and `TimeSpan.FromSeconds` call sites in `OrderProcessor.cs` if needed. Verify no legacy SDK references remain in `OrderProcessor.cs`, `ShipmentPipeline.cs`, or `Program.cs`.
7. Restore and build on `net472` while the unused legacy provider/package still exist for the final cleanup boundary.

## Research Findings
- **Project and dependency shape:** `servicebus-sample/Contoso.Ordering.csproj` is an SDK-style executable targeting only `net472`, has no project references, and declares `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1 directly. The legacy package and `MessagingFactoryProvider.cs` remain untouched for the final cleanup subtask, but no runtime path in `Program.cs` will use them after this subtask.
- **Assessment:** the project assessment reports nine source-compatibility findings and no incompatible packages. The findings relevant here are the `TimeSpan.FromMinutes(double)` lock-renewal configuration and `TimeSpan.FromSeconds(double)` batch-receive wait in `OrderProcessor.cs`; explicit `double` literals will keep overload selection stable for the later `net10.0` cutover. No assessment feature requires a different inbound design.
- **Queue processor inventory:** the production `Start` pump uses PeekLock, manual settlement, four concurrent callbacks, and a one-minute automatic lock-renewal window. Its handler completes successes, dead-letters invalid payloads, dead-letters other failures at delivery count 5 or above, and abandons earlier failures. The alternate asynchronous pump uses eight concurrent callbacks, the legacy five-minute default renewal window, and abandons every failure. The migrated class will expose asynchronous startup for both modes and preserve their distinct settlement policies.
- **Queue receiver inventory:** `DrainAsync` waits up to five seconds for at most the requested number of messages, deserializes each through the order contract, and completes each successfully drained PeekLock message. `PeekAsync` returns one deserialized message without settlement. Both operations will use a `ServiceBusReceiver` created from the same `ServiceBusClient` as the processor.
- **Subscription inventory:** `ShipmentSubscriber` uses PeekLock, manual settlement, and two concurrent callbacks. It reads the `carrier` application property, completes successful callbacks, dead-letters callback failures with reason `ShipmentHandlerFailed`, and allows deserialization failures to escape the handler so the processor abandons them for retry.
- **Track 2 mapping verified:** the official Azure Service Bus migration guide maps push receive to `ServiceBusProcessor`, pull receive/peek to `ServiceBusReceiver`, and settlement to `ProcessMessageEventArgs`/the receiver. It also documents graceful asynchronous shutdown through `StopProcessingAsync`. Package XML confirms PeekLock is the processor default, both generations default automatic lock renewal to five minutes, and Track 2 exposes explicit `AutoCompleteMessages`, `MaxConcurrentCalls`, `MaxAutoLockRenewalDuration`, and `ReceiveMode` options.
- **Composition and lifetime:** `Program.cs` already owns one process-wide modern `ServiceBusClient` for outbound work. Inbound classes will create their processors/receiver from that same client, own and asynchronously dispose those child resources, and expose awaited start/stop methods. This removes runtime construction and disposal of `MessagingFactoryProvider`, `QueueClient`, and `SubscriptionClient`.
- **Serialization and properties:** all inbound paths will use `OrderMessageContract.Read(ServiceBusReceivedMessage)`, matching the modern JSON contract established by task 01. Legacy `Properties` access becomes `ApplicationProperties`; malformed JSON is treated as poison on the standard queue path, while the alternate queue pump and shipment deserialization retain their existing abandon/retry behavior.
- **Stubs and tests:** no `// STUB:` markers and no test project are present. Live settlement, lock renewal, and processor shutdown require an Azure Service Bus namespace and therefore cannot be exercised without credentials.
- **Package action and build plan:** no package edit is required in this subtask. Restore and build the complete project in Release for `net472` with `dotnet`, require zero warnings/errors, verify the output executable, statically confirm the three migrated runtime files contain no legacy namespace/client usage, and run `git diff --check`.

## Done when
All queue and subscription receive paths use modern processors/receivers, settlement and lifecycle behavior is preserved, `Program.cs` uses only the modern client at runtime, and the intermediate project builds with zero errors.

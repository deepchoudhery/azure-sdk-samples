# 01-upgrade-contoso-ordering.03-outbound-messaging: Migrate queue sending and topic publishing to modern Service Bus senders

## Objective
Migrate all outbound queue and topic paths to `ServiceBusSender` and wire them into the application while retaining legacy inbound processing temporarily.

## Scope
- `servicebus-sample/OrderSender.cs`
- Outbound `ShipmentPublisher` code in `servicebus-sample/ShipmentPipeline.cs`
- `servicebus-sample/Program.cs`
- The shared message serialization helper created by task 01 only if a narrowly scoped correction is required

## Steps
1. Replace `BrokeredMessage`, `QueueClient`, and `TopicClient` outbound usage with `ServiceBusMessage` and `ServiceBusSender`.
2. Preserve message identifiers, correlation ID, subject/label semantics, content type, TTL, scheduled enqueue time, session ID, and application properties. Use the shared `OrderMessage` body serializer.
3. Replace synchronous sends with explicit async methods and update callers accordingly. Implement batching with `ServiceBusMessageBatch.TryAddMessage`, sending full batches and creating subsequent batches as necessary instead of relying on a fixed count.
4. Preserve scheduled delivery semantics using the modern API and `DateTimeOffset` conversion where required.
5. Update `Program.cs` to create and own the modern `ServiceBusClient` and the outbound senders while leaving the still-legacy queue/subscription receivers wired through the legacy provider until the inbound subtask. Ensure async disposal/closure is correct for migrated outbound resources.
6. Resolve the assessment-reported `TimeSpan.FromHours` call site in `OrderSender.cs` if needed and verify no legacy SDK references remain in the migrated outbound classes.
7. Restore and build on `net472`.

## Research Findings
- **Project and dependency shape:** `servicebus-sample/Contoso.Ordering.csproj` is an SDK-style executable targeting only `net472`, has no project references, and declares package versions locally. Its direct Service Bus references are `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1; both must remain in this subtask because outbound code is moving to Track 2 while `OrderProcessor`, `ShipmentSubscriber`, and `MessagingFactoryProvider` remain on the legacy inbound API.
- **Outbound inventory:** `OrderSender` currently owns a legacy `QueueClient` and has five send paths: a metadata-rich synchronous send, a simpler asynchronous send, count-based batch sends, scheduled delivery, and ordered session sends. `ShipmentPublisher` owns a legacy `TopicClient` and publishes shipment messages. No legacy client types escape through these classes' public method signatures except their constructors.
- **Composition boundary:** `Program.cs` currently obtains both outbound and inbound clients from one legacy `MessagingFactoryProvider`. It must instead create one process-wide `ServiceBusClient`, create queue/topic `ServiceBusSender` instances from it, and continue using the legacy provider only for the queue receiver and subscription client. The modern client and both senders implement asynchronous disposal; the legacy provider and inbound clients retain their existing synchronous shutdown path.
- **Message contract:** `OrderMessageContract.CreateMessage` already centralizes the modern JSON body and `application/json` content type. Every migrated path will create its message through this helper, then copy the existing message ID, correlation ID where present, label as `Subject`, TTL, session ID, and application properties. This intentionally replaces the legacy serializer/content-type mismatch (`BrokeredMessage(order)` plus `application/xml`) with the shared modern JSON contract established by the prerequisite task.
- **Batching:** the official Service Bus migration guide replaces fixed-count `SendBatchAsync` usage with `CreateMessageBatchAsync`, `ServiceBusMessageBatch.TryAddMessage`, and `SendMessagesAsync`. A message rejected by an empty batch is individually too large and must fail explicitly; otherwise the full batch is sent and a fresh batch is created.
- **Scheduling:** Track 2 uses `ServiceBusSender.ScheduleMessageAsync` and a `DateTimeOffset`. Because the existing parameter is explicitly named `enqueueAtUtc`, its ticks will be treated as UTC during conversion rather than allowing an unspecified `DateTime` to be interpreted as local time.
- **Assessment issues:** the project assessment reports nine potential source incompatibilities, with the only issue in this task's files being `TimeSpan.FromHours(double)` in `OrderSender.cs`. The migrated TTL will use an explicit `double` literal to keep overload binding stable for the later `net10.0` cutover. The assessment lists `Azure.Messaging.ServiceBus` 7.20.2 and its dependencies as compatible.
- **Protocol constraint:** the modern Service Bus client uses AMQP. The current legacy factory is already explicitly configured for AMQP on its split SAS path, and no NetMessaging-only outbound behavior was found.
- **Package action:** no package edit is required. The modern package is already present for senders and the legacy package must remain until the inbound-processing subtask.
- **Stubs and tests:** no `// STUB:` markers and no test project were found. Runtime sends cannot be exercised without a Service Bus namespace credential, so validation is restore/build plus static verification of the migrated outbound classes and output assembly.
- **Validation plan:** restore `Contoso.Ordering.csproj`, build the complete project in Release for `net472` with zero warnings/errors, verify the output executable, confirm the outbound classes contain no `BrokeredMessage`, legacy `QueueClient`, or legacy `TopicClient`, and run `git diff --check`.

## Done when
Queue sending and shipment publishing use only modern Service Bus data-plane APIs, all outbound behaviors are represented, the mixed modern-outbound/legacy-inbound composition is buildable, and the project builds with zero errors.

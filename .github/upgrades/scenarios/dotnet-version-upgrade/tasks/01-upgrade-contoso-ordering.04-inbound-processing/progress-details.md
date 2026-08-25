# Progress Details

## Status
Completed `01-upgrade-contoso-ordering.04-inbound-processing`.

## Research
- Read the complete task, scenario instructions, execution-stage instructions, Azure client-library migration skill, build skill, Service Bus service map, and official Service Bus Track 1-to-Track 2 migration guide before editing source.
- Confirmed the project is an SDK-style `net472` executable with no project references or test project. It directly references `Azure.Messaging.ServiceBus` 7.20.2 and temporarily retains `WindowsAzure.ServiceBus` 6.2.1 for the later cleanup boundary.
- Inventoried both queue pumps, pull drain/peek operations, subscription handling, settlement rules, concurrency, lock renewal, application properties, and ownership in `Program.cs`.
- Verified from package XML that legacy and modern default auto-lock renewal are both five minutes and that the modern processor supports explicit PeekLock, manual completion, concurrency, and lock-renewal options.
- Confirmed the assessment reports no incompatible packages; the relevant API findings are the `TimeSpan.FromMinutes(double)` and `TimeSpan.FromSeconds(double)` call sites.
- Confirmed no `// STUB:` markers exist.
- Added the complete findings and validation plan to `task.md` before source edits.

## Implementation
- Replaced both legacy queue pumps with `ServiceBusProcessor` startup modes:
  - Standard mode preserves four concurrent calls, one-minute lock renewal, manual settlement, poison dead-lettering, delivery-count retry budget, region property reporting, and PeekLock.
  - High-concurrency mode preserves eight concurrent calls, the legacy five-minute default renewal duration, manual completion, and abandon-on-failure behavior.
- Replaced queue batch receive and peek with a lazily created PeekLock `ServiceBusReceiver`; drained messages are explicitly completed and all bodies use `OrderMessageContract.Read`.
- Replaced `ShipmentSubscriber` with a modern subscription processor preserving two concurrent calls, five-minute lock renewal, PeekLock, manual completion, carrier application-property handling, callback-failure dead-lettering, and retry behavior for deserialization failures.
- Converted processor/subscriber start and stop operations to awaited asynchronous lifecycle methods and added asynchronous disposal.
- Updated `Program.cs` so the existing process-wide `ServiceBusClient` creates all senders, processors, and receivers. Removed runtime use of `MessagingFactoryProvider` and all legacy Service Bus types from `Program.cs`.
- Kept `MessagingFactoryProvider.cs`, the legacy package reference, and the `net472` target unchanged for the final cleanup subtask.

## Files Modified
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.04-inbound-processing\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.04-inbound-processing\progress-details.md`
- `servicebus-sample\OrderProcessor.cs`
- `servicebus-sample\ShipmentPipeline.cs`
- `servicebus-sample\Program.cs`

## Validation
Executed:

```text
dotnet restore .\servicebus-sample\Contoso.Ordering.csproj --verbosity minimal
dotnet build .\servicebus-sample\Contoso.Ordering.csproj --framework net472 --no-restore --configuration Release --verbosity minimal
```

Results:
- Restore succeeded.
- Complete Release build for `net472` succeeded with **0 warnings and 0 errors**.
- Output verified at `servicebus-sample\bin\Release\net472\Contoso.Ordering.exe`.
- Static verification found no `Microsoft.ServiceBus`, `MessagingFactoryProvider`, legacy queue/subscription client, `BrokeredMessage`, or legacy pump invocation in `OrderProcessor.cs`, `ShipmentPipeline.cs`, or `Program.cs`.
- Package graph still resolves `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1 as required for this intermediate boundary.
- `git diff --check` passed.

No live Service Bus validation was run because `CONTOSO_SERVICEBUS_CONNECTION` is not configured. Live verification of settlement, lock renewal, drain/peek behavior, and graceful processor shutdown remains dependent on an Azure Service Bus namespace. No automated tests were run because the repository contains no test project and scenario test coverage is set to Skip.

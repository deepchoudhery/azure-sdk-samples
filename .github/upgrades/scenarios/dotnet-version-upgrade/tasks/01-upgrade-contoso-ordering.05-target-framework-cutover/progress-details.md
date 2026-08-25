# Progress details

## Status
Completed the irreversible .NET 10 cutover for `Contoso.Ordering`.

## Changes
- Enriched `task.md` with the pre-edit dependency, framework, legacy-usage, assessment-finding, and validation research.
- Changed `servicebus-sample/Contoso.Ordering.csproj` from the single `net472` target to the single `net10.0` target.
- Removed the obsolete .NET Framework binding redirect properties and legacy-SDK project comment.
- Removed the direct `WindowsAzure.ServiceBus` 6.2.1 reference and retained `Azure.Messaging.ServiceBus` 7.20.2.
- Deleted `servicebus-sample/MessagingFactoryProvider.cs`, which contained the final active Track 1 Service Bus code.
- Updated `servicebus-sample/OrderMessage.cs` documentation to remove the obsolete `BrokeredMessage` side-by-side-migration reference while preserving the data contract annotations.

## Assessment disposition
- Reviewed all nine reported `TimeSpan.From*` findings. The provider finding was removed with `MessagingFactoryProvider.cs`.
- The eight surviving reported calls in `TopologyManager.cs`, `OrderSender.cs`, and `OrderProcessor.cs` explicitly use `double` literals, selecting the intended overload unambiguously on .NET 10.
- The complete project compilation also validated the other `TimeSpan` uses that were not part of the nine assessment findings.

## Validation
- `dotnet restore .\servicebus-sample\Contoso.Ordering.csproj --verbosity minimal`
  - Succeeded.
- `dotnet build .\servicebus-sample\Contoso.Ordering.csproj --configuration Release --framework net10.0 --no-restore --verbosity minimal`
  - Succeeded with **0 warnings and 0 errors**.
  - Produced `servicebus-sample\bin\Release\net10.0\Contoso.Ordering.dll`.
- Full `*.cs`, `*.csproj`, `*.props`, and `*.targets` scan under `servicebus-sample` found no legacy Service Bus namespaces, package names, or Track 1 types.
- `dotnet list .\servicebus-sample\Contoso.Ordering.csproj package --include-transitive` reports only `Azure.Messaging.ServiceBus` 7.20.2 as a top-level package for `net10.0`; `WindowsAzure.ServiceBus`, `Microsoft.ServiceBus`, and its Track 1 dependency chain are absent.
- `servicebus-sample\obj\project.assets.json` contains no `WindowsAzure.ServiceBus`, `Microsoft.ServiceBus`, `Microsoft.Azure.Services.AppAuthentication`, or `Microsoft.Rest.ClientRuntime` entries.

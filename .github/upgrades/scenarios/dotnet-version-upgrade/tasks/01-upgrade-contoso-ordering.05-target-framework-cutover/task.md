# 01-upgrade-contoso-ordering.05-target-framework-cutover: Remove the legacy SDK and cut the project over to net10.0

## Objective
Complete the irreversible project cutover after every Service Bus concern has been migrated: remove legacy artifacts, target .NET 10 in place, and resolve remaining framework compatibility issues.

## Scope
- `servicebus-sample/Contoso.Ordering.csproj`
- `servicebus-sample/MessagingFactoryProvider.cs`
- All C# files under `servicebus-sample/` for legacy-usage and remaining assessment checks

## Steps
1. Confirm no active source path uses `Microsoft.ServiceBus`, `Microsoft.ServiceBus.Messaging`, `MessagingFactory`, `NamespaceManager`, `BrokeredMessage`, or other `WindowsAzure.ServiceBus` types.
2. Delete the now-unused `MessagingFactoryProvider.cs` and remove the `WindowsAzure.ServiceBus` 6.2.1 package reference. Keep `Azure.Messaging.ServiceBus` at the compatible assessed version.
3. Change `Contoso.Ordering.csproj` from `net472` to `net10.0` in place. Remove .NET Framework-only binding redirect settings and stale comments/settings that no longer apply. Do not multi-target.
4. Restore under .NET 10 and fix all compile errors inline. Review all 9 assessment-reported `TimeSpan` occurrences across `TopologyManager.cs`, `OrderSender.cs`, `OrderProcessor.cs`, and `MessagingFactoryProvider.cs`; the deleted provider eliminates its occurrence, and each remaining call must be valid and unambiguous on net10.0.
5. Search the full project for legacy namespaces/types and confirm the old package is absent from the resolved dependency graph.
6. Build the complete project on `net10.0`.

## Done when
The project targets only `net10.0`, the legacy provider and package are removed, no legacy Service Bus API references remain, all assessment findings are resolved or proven non-applicable, restore succeeds, and the full project builds with zero errors.

## Research and execution details

### Current state
- `Contoso.Ordering.csproj` is SDK-style, targets only `net472`, and manages package versions directly (no `Directory.Packages.props` or `Directory.Build.props` is present).
- The project directly references `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1. The resolved `net472` graph confirms the old package also brings in Track 1 dependencies such as `Microsoft.Azure.Services.AppAuthentication`, `Microsoft.IdentityModel.Clients.ActiveDirectory`, `Microsoft.Rest.ClientRuntime`, and `Microsoft.ServiceBus`.
- The .NET 10 SDK is installed. This is a modern SDK-style console project with no solution/project dependencies or special desktop/resource build requirements, so `dotnet restore` and `dotnet build` against the project are the correct validation tools.
- The only active legacy namespace/type usages are isolated to `MessagingFactoryProvider.cs`. The XML mention of `BrokeredMessage` in `OrderMessage.cs` is migration-history documentation rather than an active API use and should be removed so the final full-project legacy scan is clean.
- The existing project comment about `Microsoft.ServiceBus.dll` and the `AutoGenerateBindingRedirects` / `GenerateBindingRedirectsOutputType` properties are .NET Framework-only artifacts.

### Assessment finding disposition
- The assessment's nine `TimeSpan.From*` findings were captured before the Track 2 source migration. Eight surviving calls are now in `TopologyManager.cs`, `OrderSender.cs`, and `OrderProcessor.cs`; each uses an explicit `double` literal (`1d`, `5d`, `7d`, `10d`, or `12d`) so the intended `TimeSpan.From*(double)` overload remains unambiguous on .NET 10.
- The ninth assessment finding is `MessagingFactorySettings.OperationTimeout` in `MessagingFactoryProvider.cs`; deleting the obsolete provider removes it.
- Additional `TimeSpan` uses in method signatures and `ShipmentPipeline.cs` are not assessment findings. They are already valid on .NET 10; all will still be compiler-validated during the complete project build.

### Planned edits and validation
1. Replace the singular `TargetFramework` value with `net10.0`; remove the stale legacy-SDK comment and Framework-only binding redirect properties.
2. Remove the `WindowsAzure.ServiceBus` reference while retaining assessed `Azure.Messaging.ServiceBus` 7.20.2.
3. Delete `MessagingFactoryProvider.cs` and remove the obsolete legacy-type documentation mention.
4. Restore, build the complete project, scan all project source/project files for legacy namespaces/types, and inspect the resolved dependency graph to prove `WindowsAzure.ServiceBus` and its Track 1 dependency chain are absent.
5. Record exact changes and validation evidence in `progress-details.md`.

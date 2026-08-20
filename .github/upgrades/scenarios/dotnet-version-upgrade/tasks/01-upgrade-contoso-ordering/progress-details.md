# Progress: 01-upgrade-contoso-ordering

## Status

Ready. The project was upgraded in place to `net10.0`, migrated to the modern Azure Service Bus SDK, and validated successfully.

## Research completed

- Loaded the target-framework, package-management, Azure Service Bus migration, and build skills before source changes.
- Evaluated the project-scoped MSBuild dependency model: standard non-CPM package management, no imported central package files, no project references, and `WindowsAzure.ServiceBus` 6.2.1 defined directly in `Contoso.Ordering.csproj`.
- Queried the project assessment source and detailed report: one incompatible package, nine `TimeSpan` source-compatibility findings, one `Uri` behavioral finding, and no additional detected technology features.
- Queried the configured NuGet feed and selected stable `Azure.Messaging.ServiceBus` 7.20.2. Its administration API is included in the same package.
- Recorded these findings and the implementation/validation plan in `task.md` before editing source.

## Changes completed

- Retargeted `Contoso.Ordering.csproj` from `net472` to `net10.0`; removed .NET Framework binding-redirect settings and warning suppressions.
- Replaced `WindowsAzure.ServiceBus` 6.2.1 with `Azure.Messaging.ServiceBus` 7.20.2.
- Replaced `MessagingFactory` with one async-disposable `ServiceBusClient` owner and preserved both connection-string and shared-access-key authentication paths.
- Replaced legacy send, receive, processor, settlement, scheduling, batching, and administration APIs with the modern SDK equivalents.
- Made the JSON wire-format decision explicit on both sending and receiving paths.
- Replaced count-based batching with size-aware `ServiceBusMessageBatch.TryAddMessage`.
- Preserved the legacy `TimeSpan` behavior by explicitly selecting the `double` overloads.
- Replaced legacy namespace URI construction with explicit namespace/`sb://` endpoint validation and normalization.
- Preserved legacy simple namespace configuration by expanding names such as `contoso` to `contoso.servicebus.windows.net`, while leaving fully qualified hostnames unchanged.
- Cached the project build-tool decision in `scenario-instructions.md`.

## Validation

- `dotnet restore servicebus-sample/Contoso.Ordering.csproj --force --no-cache`: succeeded.
- Stable SDK validation via .NET SDK 10.0.303 MSBuild, Release rebuild, and `TreatWarningsAsErrors=true`: succeeded with zero errors and zero warnings; output is `servicebus-sample/bin/Release/net10.0/Contoso.Ordering.dll`.
- Package audit: no vulnerable direct or transitive packages reported by the configured sources.
- Legacy-symbol scan: no `Microsoft.ServiceBus`, `BrokeredMessage`, `MessagingFactory`, `NamespaceManager`, legacy callback invocation, `WindowsAzure.ServiceBus`, `net472`, binding-redirect property, or `NoWarn` usage remains in project/source files.
- Automated tests: no tracked test project targets `Contoso.Ordering`.
- Offline Release smoke check with `CONTOSO_SERVICEBUS_CONNECTION` unset: exited successfully with code 0 and did not contact Azure.
- Live Service Bus integration validation was not run because no namespace credential was supplied.

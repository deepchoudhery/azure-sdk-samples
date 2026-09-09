# .NET Version Upgrade Plan

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | The scope contains one independent SDK-style .NET Framework project with no dependencies or dependants, so the single-project framework migration rule applies. |
| Project Approach | In-place | `Contoso.Ordering.csproj` is the sole console application in scope and has no remaining .NET Framework consumers that require parallel targeting. |
| Unsupported Packages | Resolve Inline | The assessment found one incompatible package, `WindowsAzure.ServiceBus`, with the identified replacement `Azure.Messaging.ServiceBus`. |
| Unsupported API Handling | Fix Inline | The assessment found a bounded set of 10 API compatibility issues across five files, allowing all required changes to be completed without deferred stubs. |
| Test Coverage | Skip | The confirmed planning decision is to omit test-baseline and test-generation work from this upgrade. |

### Selected Strategy
**All-at-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: The scoped workload is one SDK-style `net472` console project with no project dependencies, so the framework migration rules prescribe a single atomic upgrade.

### 01-upgrade-contoso-ordering: Upgrade Contoso.Ordering to .NET 10

Upgrade only `servicebus-sample/Contoso.Ordering.csproj` and its existing source files in place from `net472` to `net10.0`. Because this is a single, already SDK-style project, this task includes prerequisite SDK and repository configuration checks, the target-framework change, package migration, inline compatibility remediation, and final project validation. Do not modify sibling sample projects, add test-baseline work, deploy or delete infrastructure, or create a pull request.

The assessment identifies one incompatible package (`WindowsAzure.ServiceBus`) that must be replaced with `Azure.Messaging.ServiceBus`, plus nine source-incompatible API findings and one behavioral-change finding across five files. Before editing, inspect the Service Bus client creation, send/receive pumps, topology administration, message settlement, and serialization paths documented in `servicebus-sample/README.md`; preserve queue/topic behavior, retry and settlement semantics, SAS and connection-string authentication paths, and the existing message wire format unless an intentional compatible migration is established. Remove obsolete .NET Framework-only project settings where the new target no longer supports or needs them, and keep all package and API resolution within this task rather than creating stubs or deferred follow-up work.

**Done when**: `Contoso.Ordering.csproj` targets `net10.0`, restores and builds with zero errors and zero warnings using the selected .NET SDK, no `WindowsAzure.ServiceBus` reference or legacy `Microsoft.ServiceBus` API usage remains in the scoped project, the documented Service Bus behaviors are preserved, and no out-of-scope project or infrastructure resource has been changed.

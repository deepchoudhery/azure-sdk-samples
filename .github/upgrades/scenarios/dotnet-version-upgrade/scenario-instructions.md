# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (LTS)

## Source Control
- **Source Branch**: deepchoudhery-service-bus-net10-trial
- **Working Branch**: upgrade-servicebus-dotnet-10
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Disabled

## User Preferences
### Custom Instructions
- Scope all upgrade work to `servicebus-sample\Contoso.Ordering.csproj`.
- Do not modify sibling sample projects.
- Preserve application behavior.
- Preserve the binary XML `DataContract` message wire format.
- Preserve explicit message settlement behavior; disable Track 2 auto-completion where legacy handlers settle messages.
- Treat Service Bus topology administration as data-plane work; do not introduce Azure Resource Manager packages or APIs.
- Apply the installed `migrating-azure-sdk-to-track2` skill, including `track2-contract.md`, `package-catalog.md`, `authentication.md`, and `behavior-audit.md`.
- Validate with the targeted project build and any existing affected tests, without live Azure credentials.
- Do not deploy or delete infrastructure.
- Do not create a pull request in this inner run.
- Do not modify or reinstall shared plugins or configuration.

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Project Approach: In-place

### Compatibility
- Unsupported Packages: Resolve Inline (1 incompatible package)
- Unsupported API Handling: Fix Inline

### Reliability
- Test Coverage: Skip

## Strategy
**Selected**: All-at-Once
**Rationale**: The scope contains one independent SDK-style `net472` console project with no dependencies or dependants, so the single-project .NET Framework migration rule requires an atomic upgrade.

### Execution Constraints
- Perform one atomic in-place upgrade scoped exclusively to `servicebus-sample\Contoso.Ordering.csproj` and its existing source files.
- Verify the .NET 10 SDK and applicable repository configuration before editing, then validate the scoped project with restore and build after all upgrade changes are complete.
- Resolve the incompatible `WindowsAzure.ServiceBus` package and all package/API compatibility work inline; do not introduce stubs or deferred resolution tasks.
- Preserve application and Service Bus messaging behavior, and do not modify sibling sample projects, deploy or delete infrastructure, or create a pull request.
- Skip test-baseline and test-generation work; use a single commit at the end of the atomic upgrade.

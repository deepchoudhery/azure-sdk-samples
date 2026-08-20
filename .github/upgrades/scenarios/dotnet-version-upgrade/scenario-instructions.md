# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (LTS) (`net10.0`)

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10-2
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Project Approach: In-place

### Compatibility
- Unsupported Packages: Resolve Inline (1 incompatible package)
- Unsupported API Handling: Fix Inline

## Strategy
**Selected**: All-at-Once
**Rationale**: The upgrade contains one independent SDK-style project with no project references, so dependency phasing would add no benefit.

### Execution Constraints
- Treat the framework, package, and source changes as one atomic upgrade of `Contoso.Ordering.csproj`.
- Apply the in-place target framework change; do not create a parallel project or introduce migration phasing.
- Resolve the incompatible package and all API compatibility findings within the upgrade task; do not leave package or API stubs for later work.
- Restore and build only after the project and source changes have been applied as one bounded pass.
- Run full project validation after the atomic upgrade and commit the completed upgrade as a single change.

## Build Tool Decisions
- **servicebus-sample/Contoso.Ordering.csproj**: `dotnet build` (SDK-style, single modern `net10.0` target, no desktop resources or Visual Studio-only build features).

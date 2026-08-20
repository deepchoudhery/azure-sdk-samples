# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (`net10.0`)
- **Project Scope**: `storage-sample\Contoso.Documents.csproj`
- **Migration Guidance**: Apply Azure SDK-appropriate package and API updates

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Reliability
- Test Coverage: Generate

## Test Baseline
- Status: enabled
- Projects:
  - `storage-sample\Contoso.Documents.csproj`
- Test projects:
  - `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`

## Build Tool Decisions
- **storage-sample\Contoso.Documents.csproj**: `dotnet build` (SDK-style `net8.0`; no special full-MSBuild requirements)
- **tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj**: `dotnet build` (SDK-style xUnit `net8.0` project)

## Strategy
**Selected**: All-at-Once
**Rationale**: The scope contains one SDK-style .NET 8 project with no project dependencies, no incompatible packages, and only seven API compatibility issues.

### Execution Constraints
- Perform the project and package/API updates as one atomic upgrade.
- Update all affected project files before restoring and building.
- Restore once, then build and fix all compilation errors in a single bounded pass.
- Validate the full scoped workspace build after the atomic upgrade.
- Run the generated pre-upgrade test baseline only after the upgrade builds successfully.

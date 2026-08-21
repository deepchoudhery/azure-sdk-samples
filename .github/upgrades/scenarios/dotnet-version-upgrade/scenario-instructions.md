# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (LTS)
- **Upgrade Strategy**: All-at-Once
- **Test Coverage**: Skip

## Source Control
- **Source Branch**: dev/decho/new-skill-storage
- **Working Branch**: upgrade-dotnet-10-3
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Reliability
- Test Coverage: Skip

## Strategy
**Selected**: All-at-Once
**Rationale**: The scope contains one SDK-style console project on .NET 8 with no project references, so the .NET 10 framework and Azure Storage SDK migrations can be performed as one atomic upgrade.

### Execution Constraints
- Perform a single atomic upgrade; do not introduce dependency tiers, phases, or per-service upgrade tasks.
- Update the target framework and all package references together before restoring dependencies.
- Use one bounded build-and-fix pass, then verify the complete Release build has zero warnings and zero errors.
- Run testing only after the atomic upgrade; Test Coverage is Skip and no generated baseline tasks should be created.
- Commit the completed atomic upgrade once, after final validation succeeds.

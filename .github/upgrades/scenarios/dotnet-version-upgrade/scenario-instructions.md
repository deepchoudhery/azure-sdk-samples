# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (`net10.0`)

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10-4
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Project Approach: In-place

### Compatibility
- Unsupported API Handling: Fix Inline

### Reliability
- Test Coverage: Skip

## Strategy
**Selected**: All-at-Once
**Rationale**: The assessment contains one SDK-style .NET Framework project with no project dependencies, so no dependency tiers or phased rollout are needed.

### Execution Constraints
- Perform the prerequisite checks, project update, package work, and compatibility fixes as one atomic upgrade.
- Replace the existing target framework in place; do not multi-target or create a parallel project.
- Resolve all API incompatibilities inline without generating deferred stubs or follow-up resolution tasks.
- Restore dependencies after updating the project and package references, then fix all compilation errors in one bounded pass.
- Validate the complete upgraded project with a successful build; do not generate a test baseline because Test Coverage is set to Skip.

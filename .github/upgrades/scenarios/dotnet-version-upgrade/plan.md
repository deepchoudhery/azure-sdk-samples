# .NET Version Upgrade Plan

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 1 project currently targets .NET Framework 4.7.2, is already SDK-style, and has no project dependencies.

## Projects

- `servicebus-sample/Contoso.Ordering.csproj` — console application, `net472` → `net10.0`

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | The assessment contains one project with no dependency graph, so phased migration would add no benefit. |
| Project Approach | In-place | The single project has no project dependants and can replace its existing target framework directly. |
| Unsupported API Handling | Fix Inline | The assessment identifies 9 source-incompatible API usages, which will be resolved within the upgrade rather than deferred. |
| Test Coverage | Skip | The confirmed selection omits generated behavior-locking tests despite the assessment recommendation. |

### 01-upgrade-contoso-ordering: Upgrade Contoso.Ordering to .NET 10

Verify the .NET 10 SDK and applicable toolchain configuration, then upgrade `servicebus-sample/Contoso.Ordering.csproj` in place from `net472` to `net10.0`. The project is already SDK-style and has no project dependencies, so SDK-style conversion, multi-targeting, and phased dependency work are out of scope.

Update the project's package references as required for the target framework and resolve all compatibility issues inline. The assessment reports 19 compatible packages and 9 source-incompatible API usages across 5 of the project's 7 code files; investigate the affected `TimeSpan` API calls and any compilation failures exposed by the framework change. Test-baseline generation is explicitly skipped.

Complete full-project validation after the atomic upgrade, including dependency restore and a clean build. Record any non-blocking deferred recommendations, but do not leave compilation stubs or unresolved compatibility work.

**Done when**: The .NET 10 SDK prerequisites are satisfied, `Contoso.Ordering.csproj` targets `net10.0` in place, dependencies restore without conflicts, all identified API incompatibilities are resolved inline, and the complete project builds with 0 errors.

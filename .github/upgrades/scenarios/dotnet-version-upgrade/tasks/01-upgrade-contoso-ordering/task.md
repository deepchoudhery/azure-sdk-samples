# 01-upgrade-contoso-ordering: Upgrade Contoso.Ordering to .NET 10

Verify the .NET 10 SDK and applicable toolchain configuration, then upgrade `servicebus-sample/Contoso.Ordering.csproj` in place from `net472` to `net10.0`. The project is already SDK-style and has no project dependencies, so SDK-style conversion, multi-targeting, and phased dependency work are out of scope.

Update the project's package references as required for the target framework and resolve all compatibility issues inline. The assessment reports 19 compatible packages and 9 source-incompatible API usages across 5 of the project's 7 code files; investigate the affected `TimeSpan` API calls and any compilation failures exposed by the framework change. Test-baseline generation is explicitly skipped.

Complete full-project validation after the atomic upgrade, including dependency restore and a clean build. Record any non-blocking deferred recommendations, but do not leave compilation stubs or unresolved compatibility work.

**Done when**: The .NET 10 SDK prerequisites are satisfied, `Contoso.Ordering.csproj` targets `net10.0` in place, dependencies restore without conflicts, all identified API incompatibilities are resolved inline, and the complete project builds with 0 errors.

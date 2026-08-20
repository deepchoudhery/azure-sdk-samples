# Progress — 2026-08-20

## Outcome

Created a deterministic xUnit regression baseline for the unchanged
`storage-sample\Contoso.Documents.csproj` (`net8.0`). The baseline contains 41 tests covering
`BlobEndpoint` URI construction and normalization, blob and queue retry settings, queue TTL and
visibility-duration forwarding, ordering, null behavior, UTF-8 payloads, and exception propagation.
No live Azure service is required.

## Files changed

- `Contoso.Documents.Tests.sln` — registers the production and test projects.
- `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`
- `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs`
- `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs`
- `.testagent\research.md`
- `.testagent\plan.md`
- `.testagent\status.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\scenario-instructions.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\plan.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-generate-test-baseline\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-generate-test-baseline\breakdown-context.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-generate-test-baseline\progress-details.md`

No file under `storage-sample` was modified.

## Validation

- `dotnet build .\Contoso.Documents.Tests.sln --no-incremental --nologo`: succeeded with
  0 warnings and 0 errors; both assemblies were produced under `bin\Debug\net8.0`.
- `dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --no-build --nologo`:
  41 passed, 0 failed, 0 skipped.
- `dotnet format .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj
  --verify-no-changes --no-restore --verbosity minimal`: passed.
- `git diff --exit-code -- storage-sample` and scoped status output: clean.
- `dotnet sln .\Contoso.Documents.Tests.sln list`: confirms both project registrations.

## Notes

The deterministic baseline verifies argument forwarding through virtual SDK seams rather than HTTP
wire serialization or Azure service acceptance. Nonzero service-populated queue counts likewise
require an emulator or live service and remain outside this no-network baseline.

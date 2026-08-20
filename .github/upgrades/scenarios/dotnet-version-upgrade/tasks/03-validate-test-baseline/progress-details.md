# Progress details

## 2026-08-20 — Final .NET 10 validation

- Confirmed both scoped SDK-style projects target only `net10.0`, with no
  `// STUB:` markers.
- Built `Contoso.Documents.Tests.sln` in Release with restore and a
  non-incremental build: **0 errors, 0 warnings**.
- Verified Release assemblies exist under each project's `bin\Release\net10.0`
  output folder.
- Ran `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj` explicitly
  for `net10.0`: **30 passed, 0 failed, 0 skipped**.
- Audited direct and transitive packages for both projects with
  `dotnet list Contoso.Documents.Tests.sln package --vulnerable
  --include-transitive`: no vulnerable packages were reported by the configured
  package sources.
- No production or test source changes were needed.

### Deferred recommendation

The machine selected preview SDK `10.0.400-preview.0.26356.102` because the
repository has no `global.json`. Although the projects compiled for `net10.0`
and tests ran on .NET 10.0.11, pin a supported stable .NET 10 SDK in
`global.json` for reproducible local and CI builds.

## 2026-08-20 — Baseline scenario reconciliation

- Compared every test at pre-upgrade commit `6d91d60` with the migrated suite.
  The baseline had **41 passing cases**; the first migrated validation had only
  **30 passing cases**.
- Restored **17 removed behavior-locking scenarios** using modern Azure SDK
  clients and Moq seams:
  - queue-name forwarding;
  - SDK exception identity propagation for queue creation, delayed enqueue,
    single and batch retrieval/deletion, lease renewal, peek, properties, and
    clear;
  - null delayed-message normalization;
  - empty batch retrieval with count and two-minute visibility forwarding;
  - stop-on-first-delete-failure batch behavior;
  - empty peek behavior;
  - successful `TryCreate` endpoint behavior;
  - exponential blob retry mode/delay/count and fixed queue retry
    mode/delay/count.
- Existing migrated tests already provided equivalent or stronger coverage for
  the other 24 baseline cases, including Unicode payload forwarding, Base64
  wire encoding, null immediate payloads, queue TTL, dequeue ordering, service
  counts, service endpoints/credentials, and blob timeout/concurrency.
- Rebuilt `Contoso.Documents.Tests.sln` in Release: **0 errors, 0 warnings**.
- Re-ran the `net10.0` test project: **47 passed, 0 failed, 0 skipped**. This is
  the reconciled 41-case baseline plus 6 additional modern-SDK compatibility
  cases; no production migration was reverted.

### Files changed for reconciliation

- `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs`
- `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\03-validate-test-baseline\progress-details.md`

## 2026-08-20 — Stable SDK release-readiness validation

- Added repository-root `global.json` with only `sdk.version` set to stable
  SDK `10.0.303`; this is the narrowest configuration that applies to
  `Contoso.Documents.Tests.sln` commands launched from the repository root.
- Confirmed `dotnet --version` returns **10.0.303** and `dotnet --info`
  identifies `Q:\azure-sdk-samples\global.json`; the previously selected
  `10.0.400-preview.0.26356.102` SDK is no longer used.
- Forced a no-cache restore of the full solution, restoring both scoped
  projects successfully.
- Built the full solution in Release with non-incremental compilation:
  **0 warnings, 0 errors**; both assemblies were emitted under
  `bin\Release\net10.0`.
- Ran all restored baseline tests for `net10.0`: **47 passed, 0 failed,
  0 skipped**.
- Audited direct and transitive packages across the restored solution:
  **no vulnerable packages**.

### Files changed for stable SDK validation

- `global.json`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\03-validate-test-baseline\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\03-validate-test-baseline\progress-details.md`

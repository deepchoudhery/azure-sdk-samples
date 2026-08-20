# 03-validate-test-baseline: Validate the upgraded build and regression baseline

Run final automated validation after the atomic upgrade. Build the full scoped workspace and re-run every test project recorded under `## Test Baseline` in `scenario-instructions.md`, treating failures as potential upgrade regressions and following the execution-stage baseline validation flow.

Document any deferred recommendations, but do not accept skipped or failing baseline tests as successful validation.

**Done when**: The full scoped workspace builds with zero errors and all recorded baseline tests pass on `net10.0`.

## Confirmed research findings

- Scoped workspace: `Contoso.Documents.Tests.sln`, containing
  `storage-sample\Contoso.Documents.csproj` and
  `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`.
- Both projects are SDK-style and currently target only `net10.0`; the test project
  directly references the storage sample. No full Visual Studio MSBuild features or
  `// STUB:` markers were found, so `dotnet` CLI validation is appropriate.
- Production packages are Azure.Data.Tables 12.11.0, Azure.Storage.Blobs 12.29.1,
  Azure.Storage.Files.Shares 12.27.1, and Azure.Storage.Queues 12.27.1.
- Test packages are Microsoft.NET.Test.Sdk 17.14.1, Moq 4.20.72, xunit 2.9.3,
  and xunit.runner.visualstudio 3.1.5. The recorded baseline contains three source
  files with 26 Fact/Theory methods.
- Validation will restore/build the full scoped solution, run the recorded test
  project explicitly for `net10.0`, inspect skipped/failed counts, and run NuGet
  vulnerability checks for both scoped projects.
- Repository-root SDK discovery currently selects preview SDK
  `10.0.400-preview.0.26356.102`; stable SDK `10.0.303` is installed, and no
  existing `global.json` exists anywhere in the repository.
- The narrowest reliable repository-wide correction is a root `global.json`
  containing only `sdk.version: 10.0.303`. Its location governs solution
  commands launched from the repository root without introducing unrelated SDK
  roll-forward or prerelease settings.

## Decomposition assessment

This is one coherent final-validation gate. The common and test breakdown hints
do not trigger: no TFM/package migration or code movement remains, there is one
test project, and validation must cover the solution and its dependent test project
together.

## Stable SDK validation evidence

- Root `global.json` pins SDK `10.0.303`; `dotnet --version` and `dotnet --info`
  from the repository root both resolve `10.0.303` and identify that file.
- A forced no-cache restore completed for both solution projects.
- The full Release solution build produced `net10.0` assemblies with 0 warnings
  and 0 errors.
- The full `net10.0` baseline completed with 47 passed, 0 failed, and 0 skipped.
- The restored solution's direct and transitive package audit reported no
  vulnerable packages.

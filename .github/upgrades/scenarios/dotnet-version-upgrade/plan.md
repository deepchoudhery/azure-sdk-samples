# .NET 10 Upgrade Plan

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Reliability
- Test Coverage: Generate

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 1 project, already on .NET 8 and SDK-style, with no project dependencies or incompatible packages.

## Project Group

- Application: `storage-sample\Contoso.Documents.csproj` (`net8.0` → `net10.0`)

### 01-generate-test-baseline: Generate the pre-upgrade regression baseline

Generate behavior-locking tests for `storage-sample\Contoso.Documents.csproj` on its current `net8.0` target without modifying production code. The project has no existing test project and the assessment recommends coverage because it found two `Api.0003` URI behavioral-change incidents and five `Api.0002` `TimeSpan.From*` source-compatibility incidents.

Load `#skill:generating-upgrade-test-baseline` and use the available `code-testing-generator` capability to select or create an appropriate test project. The baseline must cover the `StorageAccountFactory.BlobEndpoint` URI behavior and prioritize the request-option and queue-duration call sites in `StorageAccountFactory.cs` and `QueueRepository.cs`; record every resulting test project under `## Test Baseline` in `scenario-instructions.md`.

**Done when**: Generated tests compile and pass against the unchanged `net8.0` production project, and all baseline test project paths are recorded in `scenario-instructions.md`.

### 02-upgrade-contoso-documents: Upgrade the storage sample to .NET 10

Upgrade `storage-sample\Contoso.Documents.csproj` and the generated baseline test project
`tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj` together in one atomic pass. Verify
the .NET 10 SDK and any `global.json` constraints, update target frameworks, apply Azure
SDK-appropriate package and API changes, restore dependencies, and resolve the seven assessed
compatibility incidents while preserving the behavior captured by the baseline.

The assessment reports one compatible but deprecated `WindowsAzure.Storage` 9.3.3 dependency, five source-incompatible `TimeSpan.From*` call sites, and two behavioral `System.Uri` incidents. Research the supported package/API path before changing the dependency, retain the sample's intended behavior, and avoid unrelated modernization.

**Done when**: All scoped production and generated test projects target `net10.0`, restore succeeds, and the scoped workspace builds with zero errors.

### 03-validate-test-baseline: Validate the upgraded build and regression baseline

Run final automated validation after the atomic upgrade. Build the full scoped workspace and re-run every test project recorded under `## Test Baseline` in `scenario-instructions.md`, treating failures as potential upgrade regressions and following the execution-stage baseline validation flow.

Document any deferred recommendations, but do not accept skipped or failing baseline tests as successful validation.

**Done when**: The full scoped workspace builds with zero errors and all recorded baseline tests pass on `net10.0`.

# 01-generate-test-baseline: Generate the pre-upgrade regression baseline

Generate behavior-locking tests for `storage-sample\Contoso.Documents.csproj` on its current `net8.0` target without modifying production code. The project has no existing test project and the assessment recommends coverage because it found two `Api.0003` URI behavioral-change incidents and five `Api.0002` `TimeSpan.From*` source-compatibility incidents.

Load `#skill:generating-upgrade-test-baseline` and use the available `code-testing-generator` capability to select or create an appropriate test project. The baseline must cover the `StorageAccountFactory.BlobEndpoint` URI behavior and prioritize the request-option and queue-duration call sites in `StorageAccountFactory.cs` and `QueueRepository.cs`; record every resulting test project under `## Test Baseline` in `scenario-instructions.md`.

**Done when**: Generated tests compile and pass against the unchanged `net8.0` production project, and all baseline test project paths are recorded in `scenario-instructions.md`.

## Research findings

- Confirmed scope is the single SDK-style executable project
  `storage-sample\Contoso.Documents.csproj`, currently targeting `net8.0` with
  `WindowsAzure.Storage` 9.3.3. It has no project references, dependants, existing test
  projects, or solution file.
- The assessment recommends test coverage and reports eight incidents: one future TFM change,
  two `Api.0003` `System.Uri` occurrences at `StorageAccountFactory.cs:105-107`, and five
  `Api.0002` `TimeSpan.From*` occurrences in `StorageAccountFactory.cs:73,87` and
  `QueueRepository.cs:37,66`.
- Required behavior locks are the parsed `BlobEndpoint` URI and the configured blob/queue
  retry and duration options. Queue service calls require external storage, so tests should
  favor deterministic construction/configuration behavior and avoid live Azure dependencies.
- No `// STUB:` markers or repository-specific build skill were found. The production project
  is SDK-style with no WPF, WinForms, classic .NET Framework, COM, or C++ requirements, so
  `dotnet build` is the appropriate build tool.
- Decomposition assessment: atomic. The task creates one test project for one independent
  production project. The common and test breakdown hints do not trigger because there is no
  dependency chain, package replacement batch, existing test project, moved code, or test
  framework upgrade.

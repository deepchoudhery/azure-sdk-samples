# 02-upgrade-contoso-documents: Upgrade the storage sample to .NET 10

Upgrade `storage-sample\Contoso.Documents.csproj` and the generated baseline test project
`tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj` together in one atomic pass. Verify
the .NET 10 SDK and any `global.json` constraints, update target frameworks, apply Azure
SDK-appropriate package and API changes, restore dependencies, and resolve the seven assessed
compatibility incidents while preserving the behavior captured by the baseline.

The assessment reports one compatible but deprecated `WindowsAzure.Storage` 9.3.3 dependency, five source-incompatible `TimeSpan.From*` call sites, and two behavioral `System.Uri` incidents. Research the supported package/API path before changing the dependency, retain the sample's intended behavior, and avoid unrelated modernization.

**Done when**: All scoped production and generated test projects target `net10.0`, restore succeeds, and the scoped workspace builds with zero errors.

## Confirmed research

- Scope is one SDK-style console project plus its generated xUnit dependent:
  `storage-sample\Contoso.Documents.csproj` and
  `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`. Both currently target
  `net8.0`; the test project has a direct `ProjectReference` to the sample.
- No `global.json` exists. The installed SDK was validated as compatible with `net10.0`.
- The assessment contains eight production-project findings: the mandatory TFM change, five
  `TimeSpan.From*` source-compatibility incidents in `StorageAccountFactory.cs` and
  `QueueRepository.cs`, and two `System.Uri` behavioral incidents on `BlobEndpoint`.
  No technologies/features beyond the storage SDK were detected. The assessment did not index
  the generated test project, so its scope was confirmed directly.
- `WindowsAzure.Storage` 9.3.3 is the sole top-level production dependency. Its legacy APIs are
  used for blobs, queues, tables, and file shares across `StorageAccountFactory.cs`,
  `BlobRepository.cs`, `QueueRepository.cs`, `TableRepository.cs`, `DocumentEntity.cs`, and
  `FileShareRepository.cs`; no `// STUB:` markers were found.
- Replace the monolithic package with only the four used service packages:
  `Azure.Storage.Blobs` 12.29.1, `Azure.Storage.Queues` 12.27.1,
  `Azure.Storage.Files.Shares` 12.27.1, and `Azure.Data.Tables` 12.11.0.
  Construct service clients directly while preserving connection-string, shared-key, SAS,
  retry, timeout, queue-message encoding, endpoint, ETag, listing, and CRUD behavior.
- Generated tests in `StorageAccountFactoryTests.cs` and `QueueRepositoryTests.cs` lock down
  parsing/endpoints, credential modes, retry settings, queue message behavior, and constructor
  validation. They must be migrated to modern client/options/credential surfaces rather than
  weakened.
- Build tool is the cached `dotnet build` decision. Validation order is restore, scoped solution
  build, then `dotnet test` for `Contoso.Documents.Tests.csproj`, all with zero warnings.

## Code-review fix research (2026-08-20)

- Rechecked the legacy implementations against the migrated code. Legacy queue messages were
  Base64 encoded, `ListAsync` admitted only `CloudBlockBlob` results, and
  `DynamicTableEntity.Properties` excluded the four Table service system fields.
- `BlobRequestOptions.MaximumExecutionTime` was a five-minute overall operation limit, while
  `ParallelOperationThreadCount` was 4. In the modern SDK these map to cancellation tokens for
  total operation duration and `StorageTransferOptions.MaximumConcurrency` for data transfers;
  `Retry.NetworkTimeout` is only a per-network-operation timeout and is not equivalent.
- The four findings affect `StorageAccountFactory.cs`, `BlobRepository.cs`, and
  `TableRepository.cs`. Existing tests cover factory construction but not these compatibility
  details, so focused tests will be added for Base64 encoding, transfer policy, block-only blob
  filtering, and custom-only table properties.
- No `// STUB:` markers are present. The review fixes remain one coherent compatibility pass over
  one production project and its single test dependent. Decomposition remains atomic after
  evaluating `execution.md`, `breakdown-hints/common.md`, and `breakdown-hints/test.md`.
- The assessment query was retried during this pass but no longer resolved the recorded project;
  the on-disk assessment findings already captured above remain unchanged and these review findings
  were verified directly against the current and legacy source.

## Final review fix research (2026-08-20)

- Legacy `TableQuery<DocumentEntity>.Take(200)` imposed a 200-entity total cap across continuation
  segments. The modern `QueryAsync(..., maxPerPage: 200)` only limits each service page, so
  `ListForCustomerAsync` must break once 200 entities have been collected.
- Legacy `CloudBlockBlob.DownloadTextAsync()` read through a BOM-detecting `StreamReader`. The
  current direct `Encoding.UTF8.GetString` path returns a leading UTF-8 BOM as `\uFEFF`; rewinding
  the downloaded buffer and reading it with BOM detection preserves the old text behavior while
  retaining the bounded transfer path.
- Focused tests will exercise a pageable sequence containing more than 200 table entities and a
  downloaded UTF-8 payload containing a BOM. No stubs were found. These two tightly related
  compatibility corrections remain atomic after evaluating `execution.md`,
  `breakdown-hints/common.md`, and `breakdown-hints/test.md`.
- The required assessment query was retried and again could not resolve the recorded project, so
  the findings above were confirmed against the legacy source in Git and the current implementation.

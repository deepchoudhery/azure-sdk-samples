# .NET 10 Upgrade Plan

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 1 project, already SDK-style on .NET 8, with no project dependencies and no incompatible packages.

## Upgrade Options

### Strategy
- **Upgrade Strategy**: All-at-Once

### Reliability
- **Test Coverage**: Skip

## Project Group

- `storage-sample\Contoso.Documents.csproj` — SDK-style console application targeting `net8.0`; no project references or dependants.

### 01-upgrade-storage-sample: Upgrade Contoso.Documents to .NET 10 and the current Azure Storage SDKs

Upgrade `storage-sample\Contoso.Documents.csproj` and its directly related source files as one atomic unit. Verify the .NET 10 SDK is available, retarget the project from `net8.0` to `net10.0`, remove the retired `WindowsAzure.Storage` dependency and its `NU1902`/`NU1903` suppressions, and adopt only the modern packages required by the sample: `Azure.Storage.Blobs`, `Azure.Storage.Queues`, `Azure.Storage.Files.Shares`, and `Azure.Data.Tables`. Do not change sibling sample projects or introduce deployment, resource deletion, or pull-request work.

Preserve the sample's existing contracts across account initialization and all four storage services. Research the behavioral differences called out in `storage-sample\README.md`, especially non-throwing invalid connection-string handling, shared-key and SAS authentication, retry/network-timeout mapping, blob metadata and conditional requests, queue TTL/visibility argument ordering and `QueueMessageEncoding.None`, table 404/ETag/batch semantics, and the Azure Files create-before-upload requirement. Also remediate the assessment's .NET 10 compatibility findings in `StorageAccountFactory.cs`, `QueueRepository.cs`, and `BlobRepository.cs`, including the changed `TimeSpan.From*` overload resolution and URI behavior, without altering intended results.

Complete the atomic pass by restoring and building the scoped project, fixing all resulting compilation warnings and errors, and confirming that no legacy `WindowsAzure.Storage`, `CloudStorageAccount`, `CloudBlob*`, `CloudQueue*`, `CloudTable*`, `CloudFile*`, `TableOperation`, or `StorageException` usage remains. Test-baseline generation is intentionally excluded by the confirmed Test Coverage selection; validation must not contact Azure unless credentials and infrastructure are explicitly supplied outside this plan.

**Done when**: `Contoso.Documents.csproj` targets `net10.0`, uses the four supported Azure Storage packages with no obsolete vulnerability suppressions, preserves the documented storage behaviors, contains no legacy storage SDK symbols, and `dotnet build storage-sample\Contoso.Documents.csproj` completes with 0 warnings and 0 errors.

### 02-correct-blob-contracts-and-readme: Restore blob behavior contracts and update storage documentation

Correct the completed Track 2 migration within `storage-sample` only. Preserve the legacy
`BlobRepository.ListAsync` contract by returning only block blobs across every asynchronously
paged result, using the supported Track 2 `BlobItem.Properties.BlobType` value. Restore the
intended five-minute whole-operation deadline for affected blob operations with operation-level
cancellation while retaining Track 2 retry and concurrency behavior; document that
`Retry.NetworkTimeout` is a per-attempt timeout and is not equivalent to the legacy
`MaximumExecutionTime`.

Update only `storage-sample\README.md` to describe the resulting .NET 10 Track 2 sample and the
installed `migrating-azure-sdk-to-track2` skill. Remove stale before-state migration instructions,
verify and accurately document the WindowsAzure.Storage 9.3.3 queue Base64 default and why explicit
Track 2 Base64 encoding preserves interoperability, document actual public SDK type changes and
`RenewLeaseAsync` receipt usage, and state that no tests exist and live-service behavior was not
exercised. Validate with a warning-free scoped build and safe offline checks with
`CONTOSO_STORAGE_CONNECTION` removed. Do not contact Azure, deploy/delete infrastructure, modify
sibling projects, or create a pull request.

**Depends on**: `01-upgrade-storage-sample`

**Done when**: block-blob filtering and whole-operation deadlines are covered by offline evidence,
the scoped project builds with 0 warnings and 0 errors, the README accurately reflects the
completed migration, and the Track 2 behavior audit records the corrections and any unavoidable
API differences.

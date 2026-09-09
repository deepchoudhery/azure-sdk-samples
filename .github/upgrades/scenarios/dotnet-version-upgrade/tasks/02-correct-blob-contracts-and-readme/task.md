# 02-correct-blob-contracts-and-readme: Restore blob behavior contracts and update storage documentation

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

## Research and implementation detail

### Files in scope

- `storage-sample/BlobRepository.cs` — correct the blob listing and deadline behavior.
- `storage-sample/README.md` — replace the stale pre-migration checklist with completed-state
  documentation.
- This task's `task.md` and `progress-details.md` — record evidence and validation.

No sibling sample, shared skill/reference file, package configuration, infrastructure, or git history
is in scope.

### Current behavior and regression evidence

- The pre-migration `BlobRepository.ListAsync` obtained `CloudBlockBlob` values with an `as` cast,
  so append blobs, page blobs, and directory-like results were excluded on every continuation page.
  The migrated method currently adds every `BlobItem.Name`, which broadens the result contract.
- The pre-migration `StorageAccountFactory.CreateBlobClient` set
  `BlobRequestOptions.MaximumExecutionTime = TimeSpan.FromMinutes(5)` and an exponential retry policy.
  The migrated factory retained exponential retries but mapped the five-minute value only to
  `Retry.NetworkTimeout`, which has different scope.
- The migrated README is still a `net8.0`/`WindowsAzure.Storage` before-state checklist and contains
  reversed queue-encoding guidance.

### Authoritative evidence

- `WindowsAzure.Storage` 9.3.3 XML documentation
  (`Microsoft.WindowsAzure.Storage.xml`) states that
  `BlobRequestOptions.MaximumExecutionTime` is the time allotted to one API call across all REST
  requests and retries, tracked client-side.
- The installed `Azure.Core` 1.62.0 XML documentation states that
  `RetryOptions.NetworkTimeout` applies to individual network operations; it is not a whole-call
  deadline.
- The installed `Azure.Storage.Blobs` 12.29.2 XML documentation exposes cancellation tokens on the
  blob operations used here, describes `GetBlobsAsync` as a multi-request asynchronous sequence,
  and exposes `BlobItemProperties.BlobType` with `BlobType.Block`.
- `WindowsAzure.Storage` 9.3.3 XML documentation states that `CloudQueue.EncodeMessage` defaults to
  `true`. The installed `Azure.Storage.Queues` 12.27.1 XML documentation states that
  `QueueClientOptions.MessageEncoding` defaults to `None`, and identifies `Base64` as the prior-v11
  behavior that supports interoperability.
- The installed Track 2 contract requires optional cancellation tokens as the last service-method
  parameter and `AsyncPageable<T>` enumeration for segmented listings. The behavior-audit guidance
  identifies timeout-scope changes, blob/message wire contracts, public wrapper surfaces, and
  deployment-only behavior as explicit audit rows.

### Intended changes

1. Add an optional `CancellationToken` to each public asynchronous blob wrapper method. For each
   logical repository call, link caller cancellation to one five-minute `CancelAfter` source and pass
   that token through every Track 2 request/page in the call. This restores a whole-operation
   deadline while leaving Azure.Core retries enabled; the existing `Retry.NetworkTimeout` remains a
   separate per-network-operation setting.
2. Filter every asynchronously fetched listing page with
   `item.Properties.BlobType == BlobType.Block`, retaining the existing prefix and page-size hint.
3. Keep metadata, overwrite, append, copy-start, SAS, and `IfMatch`/412 behavior unchanged.
4. Rewrite the README as completed `net10.0` Track 2 documentation, including exact package/type
   mappings, queue Base64 interoperability, refreshed queue pop receipts, no-test status, safe
   no-connection-string execution, and unchecked live-service behavior.
5. Validate only offline: scoped restore/build, legacy-reference scan, static contract checks, and a
   smoke run with `CONTOSO_STORAGE_CONNECTION` removed.

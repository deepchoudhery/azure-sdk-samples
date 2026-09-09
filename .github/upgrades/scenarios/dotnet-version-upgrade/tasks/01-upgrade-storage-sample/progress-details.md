# Progress details — 01-upgrade-storage-sample

## Result

Completed the scoped atomic migration on branch `upgrade-dotnet-10-storage-sample`.
`storage-sample\Contoso.Documents.csproj` now targets `net10.0`, references only the four
required Track 2 storage packages, and builds with 0 warnings and 0 errors.

## Changes

- `Contoso.Documents.csproj`: replaced `WindowsAzure.Storage` 9.3.3 with
  `Azure.Storage.Blobs` 12.29.2, `Azure.Storage.Queues` 12.27.1,
  `Azure.Storage.Files.Shares` 12.27.1, and `Azure.Data.Tables` 12.12.0; removed
  `NU1902`/`NU1903`; retargeted `net8.0` to `net10.0`.
- `StorageAccountFactory.cs`: replaced `CloudStorageAccount` with cached Track 2 service clients;
  retained constructor and `TryCreate`, shared-key, SAS, service-client factory, and blob-endpoint
  entry points. Blob retry is exponential (3-second delay, 4 retries) with a 5-minute
  per-network-operation timeout; queue retry is fixed (2-second delay, 3 retries). Queue encoding
  is explicitly Base64.
- `BlobRepository.cs`: migrated container, block-blob, append-blob, listing, copy, SAS, metadata,
  overwrite, download, existence/delete, and If-Match behavior. The legacy concurrency setting is
  preserved as upload transfer concurrency 4.
- `QueueRepository.cs`: migrated send/receive/delete/peek/properties/clear operations, preserved the
  7-day TTL and caller-supplied visibility delay with named arguments, and returns
  `message.Update(receipt)` from lease renewal so the refreshed pop receipt is retained.
- `DocumentEntity.cs` and `TableRepository.cs`: implemented `ITableEntity`; preserved Replace/Merge
  choices, null on a 404 lookup, entity ETags, 412 handling, filtered paging, schema-less rows, and
  ordered 100-action transactions.
- `FileShareRepository.cs`: migrated share/directory/file operations; seekable and non-seekable
  uploads create the fixed-size file before uploading; preserved files-only listing and quota reads.

## Authoritative API evidence

- `Azure.Storage.Queues.xml` states `QueueClientOptions.MessageEncoding` defaults to `None` and that
  Base64 was the prior v11 behavior; the implementation explicitly selects Base64.
- The same package documentation states `QueueMessage.Update(UpdateReceipt)` applies the refreshed
  receipt after `UpdateMessageAsync`; the lease API returns that updated message.
- `Azure.Storage.Blobs.xml` states the `UploadAsync(Stream, BlobUploadOptions)` overload overwrites
  contents and replaces metadata; metadata and content type are supplied in that upload.
- `Azure.Data.Tables.xml` documents `GetEntityIfExistsAsync` through `NullableResponse<T>`,
  If-Match/412 behavior for `UpdateEntityAsync`, Replace versus Merge, and ordered atomic transaction
  responses.
- `Azure.Storage.Files.Shares.xml` documents that `CreateAsync(long)` initializes/replaces the file
  and content is written afterward by upload/range operations.
- `Azure.Core.xml` documents `RetryOptions.NetworkTimeout` as applying to individual network
  operations, not to the complete logical operation.

## Validation

- Offline restore:
  `dotnet restore storage-sample\Contoso.Documents.csproj -p:RestoreSources= -p:RestoreIgnoreFailedSources=true`
  — succeeded from the local package cache.
- Targeted clean build:
  `dotnet build storage-sample\Contoso.Documents.csproj --no-restore --no-incremental`
  — succeeded, **0 warnings, 0 errors**; output is under `bin\Debug\net10.0`.
- Safe smoke run with `CONTOSO_STORAGE_CONNECTION` removed — exited 0 after printing the existing
  no-credential/no-network notice.
- Invalid connection-string smoke run — `TryCreate` path printed the existing validation message and
  exited 1 without contacting Azure.
- In-memory reflection probes — invalid `TryCreate` returned false/null; shared-key and SAS paths
  created blob, queue, table, and file service clients with the expected HTTPS
  `*.core.windows.net` endpoints; a SAS beginning with `?` was accepted after normalization; queue
  Base64 and retry values and blob retry/network-timeout values matched the intended options.
- Source semantic checks — confirmed named queue TTL/visibility mapping, refreshed pop receipt,
  create-before-upload ordering, table 404/ETag/100-action mappings, and blob metadata/If-Match/
  overwrite mappings.
- `dotnet format --verify-no-changes --no-restore` — succeeded.
- Legacy scans over `*.cs` and `*.csproj` found no `WindowsAzure.Storage`, `CloudStorageAccount`,
  `CloudBlob*`, `CloudQueue*`, `CloudTable*`, `CloudFile*`, `TableOperation`, `StorageException`,
  `NU1902`, or `NU1903`.
- No tests exist for this sample; no empty `dotnet test` run was reported as test success.

## Public entry-point audit

- `StorageAccountFactory`: constructor, `TryCreate`, `FromSharedKey`, `FromSasToken`,
  `CreateBlobClient`, `CreateQueueClient`, `CreateTableClient`, `CreateFileClient`, `BlobEndpoint`.
- `BlobRepository`: constructor, `InitializeAsync`, `UploadAsync`, `UploadTextAsync`,
  `DownloadAsync`, `DownloadTextAsync`, `ExistsAsync`, `DeleteAsync`, `ListAsync`,
  `AppendAuditLineAsync`, `CopyAsync`, `GetReadSasUri`, `TryUpdateIfUnchangedAsync`.
- `QueueRepository`: constructor, `InitializeAsync`, `EnqueueAsync`, `EnqueueDelayedAsync`,
  `DequeueAsync`, `DequeueBatchAsync`, `RenewLeaseAsync`, `PeekAsync`,
  `GetApproximateLengthAsync`, `ClearAsync`.
- `TableRepository`: constructor, `InitializeAsync`, `InsertAsync`, `UpsertAsync`, `MergeAsync`,
  `GetAsync`, `DeleteAsync`, `ListForCustomerAsync`, `DumpPartitionAsync`, `UpsertBatchAsync`,
  `TryReplaceAsync`.
- `FileShareRepository`: constructor, `InitializeAsync`, `UploadAsync`, `DownloadAsync`,
  `ExistsAsync`, `DeleteAsync`, `ListAsync`, `GetQuotaInGigabytesAsync`.
- `DocumentEntity`: both constructors and all prior business properties remain; Track 2
  `PartitionKey`, `RowKey`, `Timestamp`, and `ETag` properties are explicit. No entry-point name was
  removed.

## Behavior audit

| Shape | Outcome | Evidence / action |
|---|---|---|
| External contract | Preserved | Queue clients explicitly use `QueueMessageEncoding.Base64`, matching the legacy v11 wire format documented by the selected package XML rather than Track 2's `None` default. Blob text remains UTF-8 through `BinaryData.FromString`; application console messages were not changed. |
| Operation contract | Changed deliberately | Blob/queue retries, metadata, overwrite, conditions, table 404/ETag/Replace/Merge/batch behavior, refreshed queue pop receipts, and file create-before-upload are preserved. Legacy blob `MaximumExecutionTime` bounded a whole operation; Track 2 exposes `Retry.NetworkTimeout` per network operation, so the same five-minute value now has the narrower documented scope. |
| Boundary/topology | Changed deliberately | All public factory/repository entry-point names remain. Their Azure SDK parameter/return types necessarily changed to Track 2; `DumpPartitionAsync` now returns `TableEntity` rows, and `RenewLeaseAsync` returns an updated `QueueMessage` so callers retain the new pop receipt. Clients remain bound to the same account service and named container/queue/table/share resources. |
| Operational | Unchecked | Connection-string, shared-key, SAS, endpoint, retry, timeout, and encoding construction was verified offline. Live Azure authorization, service compatibility, data-plane responses, copy completion, SAS use, and production network behavior were intentionally not exercised because validation was required to remain offline and no credentials/emulator were used. |

## Files modified

- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-storage-sample\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-storage-sample\progress-details.md`
- `storage-sample\Contoso.Documents.csproj`
- `storage-sample\StorageAccountFactory.cs`
- `storage-sample\BlobRepository.cs`
- `storage-sample\QueueRepository.cs`
- `storage-sample\DocumentEntity.cs`
- `storage-sample\TableRepository.cs`
- `storage-sample\FileShareRepository.cs`

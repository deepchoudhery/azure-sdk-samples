# 01-upgrade-storage-sample: Upgrade Contoso.Documents to .NET 10 and the current Azure Storage SDKs

Upgrade `storage-sample\Contoso.Documents.csproj` and its directly related source files as one atomic unit. Verify the .NET 10 SDK is available, retarget the project from `net8.0` to `net10.0`, remove the retired `WindowsAzure.Storage` dependency and its `NU1902`/`NU1903` suppressions, and adopt only the modern packages required by the sample: `Azure.Storage.Blobs`, `Azure.Storage.Queues`, `Azure.Storage.Files.Shares`, and `Azure.Data.Tables`. Do not change sibling sample projects or introduce deployment, resource deletion, or pull-request work.

Preserve the sample's existing contracts across account initialization and all four storage services. Research the behavioral differences called out in `storage-sample\README.md`, especially non-throwing invalid connection-string handling, shared-key and SAS authentication, retry/network-timeout mapping, blob metadata and conditional requests, queue TTL/visibility argument ordering and `QueueMessageEncoding.None`, table 404/ETag/batch semantics, and the Azure Files create-before-upload requirement. Also remediate the assessment's .NET 10 compatibility findings in `StorageAccountFactory.cs`, `QueueRepository.cs`, and `BlobRepository.cs`, including the changed `TimeSpan.From*` overload resolution and URI behavior, without altering intended results.

Complete the atomic pass by restoring and building the scoped project, fixing all resulting compilation warnings and errors, and confirming that no legacy `WindowsAzure.Storage`, `CloudStorageAccount`, `CloudBlob*`, `CloudQueue*`, `CloudTable*`, `CloudFile*`, `TableOperation`, or `StorageException` usage remains. Test-baseline generation is intentionally excluded by the confirmed Test Coverage selection; validation must not contact Azure unless credentials and infrastructure are explicitly supplied outside this plan.

**Done when**: `Contoso.Documents.csproj` targets `net10.0`, uses the four supported Azure Storage packages with no obsolete vulnerability suppressions, preserves the documented storage behaviors, contains no legacy storage SDK symbols, and `dotnet build storage-sample\Contoso.Documents.csproj` completes with 0 warnings and 0 errors.

## Implementation research

### Scope and dependency facts

- The scoped project is an SDK-style, single-target console application. `TargetFramework` is
  defined directly in `storage-sample\Contoso.Documents.csproj`; no `Directory.Build.props`,
  `Directory.Packages.props`, solution file, project references, or central package management file
  exists in this worktree.
- The only direct package is `WindowsAzure.Storage` 9.3.3. Its used service surfaces require exactly
  four Track 2 packages: `Azure.Storage.Blobs`, `Azure.Storage.Queues`,
  `Azure.Storage.Files.Shares`, and `Azure.Data.Tables`. Stable versions available in the offline
  NuGet cache are 12.29.2, 12.27.1, 12.27.1, and 12.12.0 respectively.
- All four replacements depend on `Azure.Core` and expose the Track 2 HTTP client shapes
  (`Response<T>`, `AsyncPageable<T>`, and `RequestFailedException`), so the Track 2 contract applies.
- The installed .NET SDK includes stable .NET 10 SDKs (10.0.203 and 10.0.303); the scoped target can
  be replaced in place from `net8.0` to `net10.0`.

### Affected files and required mappings

| File | Required migration |
|---|---|
| `Contoso.Documents.csproj` | Retarget to `net10.0`; replace `WindowsAzure.Storage` with the four cached Track 2 packages; remove `NU1902`/`NU1903` while retaining the existing `CS1591` suppression. |
| `StorageAccountFactory.cs` | Replace `CloudStorageAccount` with four cached service clients. Preserve throwing construction, non-throwing `TryCreate`, connection-string, shared-key, SAS, endpoint construction, blob exponential retry/network timeout, queue fixed retry, and `BlobEndpoint`. |
| `BlobRepository.cs` | Use container/blob/append-blob clients; carry content type and metadata in `BlobUploadOptions`; preserve overwrite behavior, listing, copy, SAS, and ETag 412 handling. |
| `QueueRepository.cs` | Use `QueueClient`; explicitly set `QueueMessageEncoding.Base64` to preserve legacy wire interoperability; keep TTL/visibility ordering; use refreshed `UpdateReceipt.PopReceipt` after visibility updates. |
| `DocumentEntity.cs` | Implement `ITableEntity`, including `DateTimeOffset? Timestamp` and `Azure.ETag ETag`, while retaining all existing public model properties and convenience accessors. |
| `TableRepository.cs` | Preserve insert/upsert replace/upsert merge, null-on-404 lookup, delete using the retrieved ETag, filtered paging, dynamic dictionary reads, 100-row transaction batches, and 412 optimistic-concurrency behavior. |
| `FileShareRepository.cs` | Use share/directory/file clients; create the fixed-size file before upload, then upload the stream; preserve download, exists/delete, files-only listing, and quota reads. |

### Verified behavioral constraints

- Package XML documentation confirms `RetryOptions.NetworkTimeout` is a per-network-operation
  timeout, not the legacy whole-operation `MaximumExecutionTime`. The migration preserves the
  configured five-minute value at the closest supported client-wide scope and does not claim a
  whole-operation ceiling.
- `QueueClientOptions.MessageEncoding` defaults to `None` in the selected package, whereas legacy
  `CloudQueueMessage(string)` interoperates using Base64 encoding. Queue clients therefore explicitly
  use `QueueMessageEncoding.Base64`.
- `QueueClient.UpdateMessageAsync` returns an `UpdateReceipt` containing the new pop receipt. Because
  Track 2 message models are immutable, the public lease-renewal entry point returns that receipt so
  callers can use the refreshed concurrency token.
- Blob upload overloads differ in overwrite behavior. Uploads use `BlobUploadOptions` (overwrite)
  for content/metadata and the explicit `overwrite: true` overload for text, matching legacy block
  blob uploads. Conditional text updates use `BlobRequestConditions.IfMatch`.
- `TableClient.GetEntityIfExistsAsync<T>` preserves the legacy null-on-404 result without using
  exception flow. `UpdateEntityAsync` and transaction actions carry entity ETags; 412 remains the
  optimistic-concurrency failure signal. Transaction batches remain capped at 100 and are submitted
  in input order.
- `ShareFileClient.CreateAsync(long)` allocates the fixed-size file and
  `ShareFileClient.UploadAsync(Stream)` writes content afterward; both calls are required.
- Public repository/factory entry-point names are retained. Where a legacy parameter type no longer
  exists, it is replaced by the corresponding Track 2 client/model type without introducing live
  service calls during construction.

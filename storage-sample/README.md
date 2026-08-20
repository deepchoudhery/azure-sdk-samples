# Storage sample — `migrating-azure-storage`

A document-management console app that uses **all four** legacy storage services: block and
append blobs for document bodies, a queue for the ingestion pipeline, a table for the metadata
index, and a file share for partner drops. It is stuck on the retired `WindowsAzure.Storage`
package.

- **TFM:** `net8.0`
- **Deprecated package:** `WindowsAzure.Storage`
- **Baseline:** `dotnet build` succeeds with 0 warnings, 0 errors.

Using all four services is deliberate: the skill says to add "only the packages the project
actually needs", and here that means all four. A sample that only used blobs would not test
whether the agent scopes packages correctly.

## What each file exercises

| File | Legacy surface under test |
|------|---------------------------|
| `StorageAccountFactory.cs` | `CloudStorageAccount.Parse` / `TryParse`, `StorageCredentials` (shared key **and** SAS), `CreateCloudBlobClient` / `CreateCloudQueueClient` / `CreateCloudTableClient` / `CreateCloudFileClient`, `BlobRequestOptions`, `QueueRequestOptions`, `ExponentialRetry`, `LinearRetry` |
| `BlobRepository.cs` | `CloudBlobContainer`, `CloudBlockBlob`, `CloudAppendBlob`, `UploadFromStreamAsync`, `DownloadToStreamAsync`, `UploadTextAsync`, `DownloadTextAsync`, `ExistsAsync`, `DeleteIfExistsAsync`, metadata, `ListBlobsSegmentedAsync` + `BlobContinuationToken`, `StartCopyAsync`, `GetSharedAccessSignature`, `AccessCondition`, `StorageException` 412 |
| `QueueRepository.cs` | `CloudQueue`, `CloudQueueMessage`, `AddMessageAsync` (plain and with TTL/visibility), `GetMessageAsync`, `GetMessagesAsync`, `DeleteMessageAsync` (both overloads), `UpdateMessageAsync` + `MessageUpdateFields`, `PeekMessageAsync`, `FetchAttributesAsync` + `ApproximateMessageCount`, `ClearAsync` |
| `TableRepository.cs` | `CloudTable`, `TableOperation.Insert` / `InsertOrReplace` / `InsertOrMerge` / `Retrieve<T>` / `Delete` / `Replace`, `TableResult`, `TableQuery` + `GenerateFilterCondition` + `CombineFilters`, `ExecuteQuerySegmentedAsync` + `TableContinuationToken`, `DynamicTableEntity` + `EntityProperty`, `TableBatchOperation`, `StorageException` 412 |
| `FileShareRepository.cs` | `CloudFileShare`, `CloudFileDirectory`, `CloudFile`, `ListFilesAndDirectoriesSegmentedAsync` + `FileContinuationToken`, `FetchAttributesAsync` + share quota |
| `DocumentEntity.cs` | `TableEntity` base class with reflection-serialized properties |

## Expected migration outcome

### Packages

- [ ] `WindowsAzure.Storage` is removed.
- [ ] `Azure.Storage.Blobs`, `Azure.Storage.Queues`, `Azure.Storage.Files.Shares`, and `Azure.Data.Tables` are all added. Note that tables come from `Azure.Data.Tables`, **not** `Azure.Storage.Tables` (which does not exist).
- [ ] The `NU1902;NU1903` suppression is no longer needed and should be dropped.

### Account initialization

- [ ] `CloudStorageAccount` is gone entirely. `StorageAccountFactory` now holds either a connection string or per-service clients.
- [ ] `TryParse` has no direct equivalent. The migration must keep the "invalid connection string does not throw" behavior — for example by catching `FormatException` / `ArgumentException` around client construction. Silently converting it to a throwing `Parse` changes `Program.Main`'s contract.
- [ ] `StorageCredentials(accountName, accountKey)` → `StorageSharedKeyCredential`.
- [ ] `StorageCredentials(sasToken)` → `AzureSasCredential` (or a SAS-bearing service URI).
- [ ] `ExponentialRetry` / `LinearRetry` → `BlobClientOptions.Retry` with `RetryMode.Exponential` / `RetryMode.Fixed`; `MaximumExecutionTime` → `Retry.NetworkTimeout`.

### Blobs

- [ ] `CloudBlobClient` → `BlobServiceClient`; `CloudBlobContainer` → `BlobContainerClient`; `CloudBlockBlob` → `BlobClient` (or `BlockBlobClient`); `CloudAppendBlob` → `AppendBlobClient`.
- [ ] `UploadFromStreamAsync` → `UploadAsync`; `DownloadToStreamAsync` → `DownloadToAsync`.
- [ ] Setting `blob.Properties.ContentType` plus `blob.Metadata[...]` before upload becomes `BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = ... }, Metadata = ... }` passed to `UploadAsync`. The separate `SetMetadataAsync` follow-up call can then be dropped — but dropping it *without* moving the metadata into the upload loses data.
- [ ] `UploadTextAsync` / `DownloadTextAsync` have no direct equivalent. Expect `UploadAsync(BinaryData.FromString(text))` and `DownloadContentAsync()` + `.Content.ToString()`.
- [ ] The `ListBlobsSegmentedAsync` do/while loop becomes `await foreach (BlobItem item in container.GetBlobsAsync(BlobTraits.Metadata, prefix: prefix))`. A migration that preserves a manual continuation-token loop has not really migrated.
- [ ] `StartCopyAsync(source)` → `StartCopyFromUriAsync(source.Uri)`.
- [ ] `GetSharedAccessSignature(policy)` → `GenerateSasUri(BlobSasPermissions.Read, expiresOn)`. This requires the client to have been built with a `StorageSharedKeyCredential`, so watch for a client built from a token credential here — it throws at runtime, not compile time.
- [ ] `AccessCondition.GenerateIfMatchCondition(etag)` → `BlobRequestConditions { IfMatch = new ETag(etag) }`.
- [ ] `catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 412)` → `catch (RequestFailedException ex) when (ex.Status == 412)`. There are **two** of these (blob and table).

### Queues

- [ ] `CloudQueueClient` → `QueueServiceClient`; `CloudQueue` → `QueueClient`.
- [ ] `AddMessageAsync(new CloudQueueMessage(text))` → `SendMessageAsync(text)`.
- [ ] `AddMessageAsync(message, ttl, delay, null, null)` → `SendMessageAsync(text, visibilityTimeout: delay, timeToLive: ttl)`. Argument order changes — a positional mistranslation here silently swaps TTL and delay.
- [ ] `GetMessageAsync` → `ReceiveMessageAsync`; `GetMessagesAsync(n, ...)` → `ReceiveMessagesAsync(n, ...)`.
- [ ] `DeleteMessageAsync(message)` → `DeleteMessageAsync(message.MessageId, message.PopReceipt)`.
- [ ] `UpdateMessageAsync(message, extension, MessageUpdateFields.Visibility)` → `UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: extension)`.
- [ ] `FetchAttributesAsync()` + `ApproximateMessageCount` → `GetPropertiesAsync()` → `.Value.ApproximateMessagesCount`.
- [ ] **Message encoding.** The new `QueueClient` base64-encodes by default while the old SDK did not. Because this app interoperates with existing queue data, the migration should either set `MessageEncoding = QueueMessageEncoding.None` or explicitly note the behavior change. Missing this is a silent runtime data bug and is the highest-value failure to catch in this sample.

### Tables

- [ ] `CloudTableClient` → `TableServiceClient`; `CloudTable` → `TableClient`.
- [ ] `DocumentEntity : TableEntity` → a class implementing `ITableEntity` (`PartitionKey`, `RowKey`, `Timestamp` as `DateTimeOffset?`, `ETag` as `ETag`). The read-only `CustomerId` / `DocumentId` convenience properties must survive.
- [ ] `TableOperation.Insert` → `AddEntityAsync`; `InsertOrReplace` → `UpsertEntityAsync(entity, TableUpdateMode.Replace)`; `InsertOrMerge` → `UpsertEntityAsync(entity, TableUpdateMode.Merge)`; `Retrieve<T>` → `GetEntityAsync<T>` (which **throws 404** rather than returning a null `TableResult.Result`, so `GetAsync` needs a `try`/`catch` or `GetEntityIfExistsAsync`); `Delete` → `DeleteEntityAsync`; `Replace` → `UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace)`.
- [ ] The string filter DSL becomes either `TableClient.CreateQueryFilter` or a LINQ expression: `QueryAsync<DocumentEntity>(d => d.PartitionKey == customerId && !d.IsArchived)`.
- [ ] `ExecuteQuerySegmentedAsync` + `TableContinuationToken` loops become `await foreach` over `AsyncPageable<T>`.
- [ ] `DynamicTableEntity` + `IDictionary<string, EntityProperty>` → `TableEntity` used as a dictionary. The `DumpPartitionAsync` return type has to change; returning `IReadOnlyList<TableEntity>` is fine.
- [ ] `TableBatchOperation` → `IEnumerable<TableTransactionAction>` + `SubmitTransactionAsync`. The 100-entity chunking must be preserved.

### File shares

- [ ] `CloudFileClient` → `ShareServiceClient`; `CloudFileShare` → `ShareClient`; `CloudFileDirectory` → `ShareDirectoryClient`; `CloudFile` → `ShareFileClient`.
- [ ] `UploadFromStreamAsync(stream)` → `Create(stream.Length)` **then** `UploadAsync(stream)`. Azure Files requires the file to be created at a fixed size first; a straight rename to `UploadAsync` without the `CreateAsync` call fails at runtime. This is the highest-value failure to catch in the file-share section.
- [ ] `DownloadToStreamAsync(buffer)` → `DownloadAsync()` then copy `.Value.Content` into the buffer.
- [ ] `ListFilesAndDirectoriesSegmentedAsync` + `FileContinuationToken` → `await foreach` over `GetFilesAndDirectoriesAsync()`, with `ShareFileItem.IsDirectory` replacing the `as CloudFile` type test.
- [ ] `FetchAttributesAsync()` + `Properties.Quota` → `GetPropertiesAsync()` → `.Value.QuotaInGB`.

### Build

- [ ] `dotnet build` succeeds.
- [ ] `git grep -n "WindowsAzure.Storage\|CloudStorageAccount\|CloudBlob\|CloudQueue\|CloudTable\|CloudFile\|TableOperation\|StorageException"` returns nothing.

## Running

```powershell
dotnet build
$env:CONTOSO_STORAGE_CONNECTION = "DefaultEndpointsProtocol=https;AccountName=..."   # optional
dotnet run
```

Without `CONTOSO_STORAGE_CONNECTION` the app prints a notice and exits without touching the
network. `UseDevelopmentStorage=true` works if you have Azurite running.

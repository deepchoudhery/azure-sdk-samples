# Storage sample

This .NET 10 console application demonstrates the supported Track 2 Azure SDK clients for the four
Azure Storage data services used by the original sample:

- `Azure.Storage.Blobs` for block blobs, append blobs, metadata, SAS generation, copy, and ETag
  concurrency.
- `Azure.Storage.Queues` for enqueue, receive, delete, visibility renewal, peek, and queue
  properties.
- `Azure.Data.Tables` for typed and dynamic entities, queries, ETags, upserts, and transactions.
- `Azure.Storage.Files.Shares` for shares, directories, file transfer, listing, and quota.

The project targets `net10.0`. Its migration was implemented and reviewed using the installed
`migrating-azure-sdk-to-track2` skill, including its Track 2 contract, package catalog,
authentication, and behavior-audit guidance.

## Track 2 API shape

The retired `WindowsAzure.Storage` 9.3.3 account façade and service-specific `Cloud*` types were
replaced with service clients:

| Legacy public SDK type | Track 2 public SDK type |
|---|---|
| `CloudStorageAccount` | Per-service clients held by `StorageAccountFactory` |
| `CloudBlobClient` / `CloudBlobContainer` | `BlobServiceClient` / `BlobContainerClient` |
| `CloudBlockBlob` / `CloudAppendBlob` | `BlobClient` / `AppendBlobClient` |
| `CloudQueueClient` / `CloudQueueMessage` | `QueueServiceClient` / `QueueMessage` |
| `CloudTableClient` / `CloudTable` / `TableEntity` | `TableServiceClient` / `TableClient` / `ITableEntity` |
| `CloudFileClient` / `CloudFileShare` / `CloudFile` | `ShareServiceClient` / `ShareClient` / `ShareFileClient` |
| `StorageException` | `RequestFailedException` |

The factory still supports connection strings, shared keys, and SAS tokens. It deliberately does
not switch authentication to a different principal as part of the SDK migration.

Some wrapper signatures necessarily changed with the public SDK models. For example,
`QueueRepository.RenewLeaseAsync` now returns a `QueueMessage` because
`QueueClient.UpdateMessageAsync` returns an `UpdateReceipt` containing a refreshed pop receipt.
The method applies that receipt with `message.Update(receipt)`; callers must use the returned message
for a later update or delete rather than keeping the stale receipt. Blob repository asynchronous
methods also accept an optional `CancellationToken` so caller cancellation can be combined with the
legacy operation deadline.

## Preserved behavior

### Blob listing and deadlines

`BlobRepository.ListAsync` preserves the original flat prefix search and 100-item page-size hint,
enumerates every asynchronous page, and returns only items whose
`BlobItem.Properties.BlobType == BlobType.Block`. Append and page blobs are not included.

The old `BlobRequestOptions.MaximumExecutionTime` was a client-side limit for one complete API call,
including all REST requests and retries. Track 2 `Retry.NetworkTimeout` instead limits an individual
network operation and is not equivalent. Each asynchronous `BlobRepository` operation therefore
creates one linked cancellation source, applies a five-minute deadline to the complete logical
operation, and passes its token through all requests and listing pages. Azure.Core retry settings
remain enabled, caller cancellation is retained, upload transfer concurrency remains four, and the
`IfMatch` ETag/HTTP 412 behavior is unchanged.

### Queue message encoding

`WindowsAzure.Storage` 9.3.3 documented `CloudQueue.EncodeMessage` with a default of `true`.
`Azure.Storage.Queues` documents the Track 2 `QueueClientOptions.MessageEncoding` default as
`QueueMessageEncoding.None`. The factory explicitly selects `QueueMessageEncoding.Base64`, matching
the legacy wire representation so existing producers, consumers, and queued messages remain
interoperable.

## Build and safe offline use

Build the scoped project:

```powershell
dotnet restore .\Contoso.Documents.csproj
dotnet build .\Contoso.Documents.csproj --no-incremental
```

There is no test project or existing automated test suite for this sample. A build is compilation
validation, not a claim that tests passed.

Running without a connection string is the safe offline smoke path:

```powershell
Remove-Item Env:CONTOSO_STORAGE_CONNECTION -ErrorAction SilentlyContinue
dotnet run --project .\Contoso.Documents.csproj --no-build
```

The program prints that there is nothing to do and exits without contacting Azure. Supplying
`CONTOSO_STORAGE_CONNECTION` makes the sample perform real data-plane operations; that mode requires
an appropriate storage account and credentials. Live-service behavior, authorization, service-side
copy completion, SAS usability, and service-specific runtime limits were not exercised during this
offline migration validation and remain **Unchecked**.

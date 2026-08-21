# Storage sample - `migrating-azure-storage`

A .NET 10 document-management console app that uses all four Azure Storage services:
block and append blobs for document bodies, a queue for ingestion, a table for the metadata
index, and an Azure Files share for partner drops.

- **TFM:** `net10.0`
- **SDK:** stable .NET SDK 10.0.303, selected by the local `global.json`
- **Packages:** `Azure.Storage.Blobs`, `Azure.Storage.Queues`,
  `Azure.Storage.Files.Shares`, and `Azure.Data.Tables`
- **Build:** `dotnet build --configuration Release`

## Track 2 behavior

| File | Behavior |
|------|----------|
| `StorageAccountFactory.cs` | Creates all four service clients from connection strings, shared keys, or SAS credentials. Blob retries are exponential; queue retries are fixed and retain unencoded legacy message payloads. |
| `BlobRepository.cs` | Uploads block blobs with headers and metadata, downloads and lists blobs, appends audit entries, starts server-side copies, creates read-only SAS URIs, and applies ETag conditions. |
| `QueueRepository.cs` | Sends immediate and delayed messages, receives and deletes single or batched messages, renews visibility, peeks, reports approximate length, and clears the queue. |
| `TableRepository.cs` | Inserts, replaces, merges, retrieves, queries, deletes, and transactionally upserts entities in chunks of 100. Missing entities return `null`, and ETag conflicts return `false`. |
| `FileShareRepository.cs` | Creates fixed-size files and uploads them in ranges, including buffering non-seekable streams, then supports download, existence, deletion, listing, and quota reads. |
| `DocumentEntity.cs` | Implements `ITableEntity` while retaining the `CustomerId` and `DocumentId` convenience properties. |

## Running

```powershell
dotnet build --configuration Release
$env:CONTOSO_STORAGE_CONNECTION = "DefaultEndpointsProtocol=https;AccountName=..."
dotnet run --configuration Release
```

Without `CONTOSO_STORAGE_CONNECTION`, the app prints a notice and exits without contacting
Azure. `UseDevelopmentStorage=true` works when Azurite is running with support for the services
used by the sample.

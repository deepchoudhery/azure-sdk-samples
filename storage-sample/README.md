# Contoso Documents storage sample

A .NET 10 document-management console app using all four service-specific Azure Storage SDKs:

- `Azure.Storage.Blobs` for document bodies and audit append blobs
- `Azure.Storage.Queues` for the ingestion pipeline
- `Azure.Data.Tables` for the metadata index
- `Azure.Storage.Files.Shares` for partner drops

`StorageAccountFactory` accepts an Azure Storage connection string and can also construct clients
from shared-key or SAS credentials. Queue messages retain their existing wire format by using
`QueueMessageEncoding.None`.

## Build and run

```powershell
dotnet build
$env:CONTOSO_STORAGE_CONNECTION = "DefaultEndpointsProtocol=https;AccountName=..."
dotnet run
```

Without `CONTOSO_STORAGE_CONNECTION`, the app exits without contacting Azure.
`UseDevelopmentStorage=true` works when Azurite is running.

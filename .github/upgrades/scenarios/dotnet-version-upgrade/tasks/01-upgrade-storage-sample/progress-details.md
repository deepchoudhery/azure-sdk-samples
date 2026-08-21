# Progress details

## Changes

- Upgraded `storage-sample/Contoso.Documents.csproj` from `net8.0` to `net10.0`.
- Added `storage-sample/global.json` to select stable SDK `10.0.303` with latest-patch roll-forward and prerelease SDKs disabled.
- Removed `WindowsAzure.Storage` 9.3.3 and the `NU1902`/`NU1903` suppressions.
- Replaced the legacy account facade with Track 2 service clients supporting connection strings, shared-key credentials, and SAS credentials. Preserved exponential blob retries, fixed queue retries, and legacy queue message wire encoding.
- Migrated blobs to pageable listing, atomic headers/metadata upload, append blocks, content download, server-side copy, read-only SAS generation with clock-skew allowance, and ETag conditional upload.
- Migrated queues to immediate/delayed sends, receive/delete, batch receive with a two-minute visibility timeout, visibility renewal, peek, properties, and clear operations.
- Migrated tables to `ITableEntity`, nullable not-found retrieval, typed and schema-less async queries, replace/merge upserts, ETag-aware deletes and updates, and transaction batches capped at 100 actions.
- Migrated Azure Files to exact-size creation followed by 4 MiB range uploads. Seekable streams upload from their current position; non-seekable streams are buffered first. Download, listing, existence, deletion, and quota behavior were retained.
- Updated the sample README to describe the .NET 10 Track 2 implementation.
- Preserved the legacy four-operation blob upload concurrency, restricted document listings to block blobs, chained renewed queue pop receipts through later renew/delete operations, and grouped table transactions by partition in batches of at most 100 actions.

## Package versions

| Package | Version |
|---------|---------|
| `Azure.Data.Tables` | 12.10.0 |
| `Azure.Storage.Blobs` | 12.29.1 |
| `Azure.Storage.Files.Shares` | 12.27.1 |
| `Azure.Storage.Queues` | 12.27.1 |

The restored graph contains no `WindowsAzure.Storage` dependency and `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages from the configured sources.

## Validation

- `dotnet restore Contoso.Documents.csproj --property:Configuration=Release --verbosity:minimal`: succeeded.
- `dotnet build Contoso.Documents.csproj --configuration Release --no-restore --verbosity:minimal`: succeeded with 0 warnings and 0 errors.
- `dotnet run --configuration Release --no-build` with no connection-string environment variable: exited successfully without contacting Azure.
- The same command with an invalid connection string: rejected it through the existing non-throwing parse path and returned exit code 1.
- Legacy package/type and vulnerability-suppression search: no matches.
- Selected SDK during validation: `10.0.303`.

## Deferred credential-dependent checks

No Azure Storage credentials or local multi-service emulator were available. The following checks remain deferred:

- Real-account and Azurite initialization for blob containers, queues, tables, and file shares.
- Shared-key and SAS client authentication for each service, including confirming that blob SAS generation is used only with a shared-key-capable client.
- Blob metadata/header persistence, async paging, append concurrency, copy completion, generated SAS access, and ETag conflict behavior against the service.
- Queue interoperability with existing unencoded messages, delayed delivery and TTL, receive/delete pop receipts, batch visibility, and lease renewal.
- Table serialization, missing-row behavior, filters, ETag conflicts, and same-partition transaction submission in 100-action chunks.
- Azure Files exact-size creation, multi-range upload (including non-seekable input), download, listing, and quota retrieval.

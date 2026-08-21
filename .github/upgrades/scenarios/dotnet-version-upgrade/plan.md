# .NET 10 Upgrade Plan

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | The scope contains one SDK-style modern .NET project with no project references, so it can be upgraded atomically. |
| Test Coverage | Skip | The confirmed selection omits generated pre-upgrade and post-upgrade test-baseline tasks. |

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: One SDK-style project currently on .NET 8, with no project references and a clear dependency structure.

## Projects

- Console application: `storage-sample/Contoso.Documents.csproj`

### 01-upgrade-storage-sample: Upgrade the storage sample to .NET 10

Upgrade `storage-sample/Contoso.Documents.csproj` atomically from `net8.0` to `net10.0`, including stable .NET 10 SDK selection where appropriate. Replace the deprecated `WindowsAzure.Storage` 9.3.3 dependency with `Azure.Storage.Blobs`, `Azure.Storage.Queues`, `Azure.Storage.Files.Shares`, and `Azure.Data.Tables`, covering the blob, queue, file-share, table, credential, retry, paging, SAS, concurrency, and transactional-batch behavior used by the sample.

The package migration is the primary risk. The executor should use `StorageAccountFactory.cs`, `BlobRepository.cs`, `QueueRepository.cs`, `TableRepository.cs`, `DocumentEntity.cs`, and `FileShareRepository.cs` as research starting points, paying particular attention to queue encoding, missing table entities, SAS scope and expiry, continuation-based enumeration, optimistic concurrency, and fixed-size Azure Files uploads. Remove the vulnerable legacy dependency chain and the `NU1902`/`NU1903` suppressions, then validate the complete project in one bounded restore/build-and-fix pass. No generated test baseline is included because Test Coverage is set to Skip.

**Done when**: The project targets `net10.0`; a stable .NET 10 SDK is selected or explicitly documented; `WindowsAzure.Storage` and its vulnerable transitive dependency chain are absent; all four replacement Azure SDK packages are referenced; `NU1902` and `NU1903` are no longer suppressed; and a Release restore and build complete with zero warnings and zero errors. Any credential-dependent behavioral checks that cannot run locally are documented as deferred validation.

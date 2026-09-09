# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (`net10.0`)

## Source Control
- **Source Branch**: deepchoudhery-storage-net10-trial
- **Working Branch**: upgrade-dotnet-10-storage-sample
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Upgrade Options

### Strategy
- **Upgrade Strategy**: All-at-Once

### Reliability
- **Test Coverage**: Skip

## Strategy
**Selected**: All-at-Once
**Rationale**: The scope contains one SDK-style console application on `net8.0`, with no project dependencies and no incompatible packages, so a single atomic upgrade to `net10.0` avoids unnecessary phasing.

### Execution Constraints
- Treat the project retargeting, Azure Storage SDK replacement, and related source changes as one atomic upgrade.
- Limit changes to `storage-sample\Contoso.Documents.csproj` and files directly related to that project; do not modify sibling sample projects.
- Preserve existing blob, queue, table, and file-share behavior while replacing the retired `WindowsAzure.Storage` API surface with supported Azure SDK clients.
- Restore and build the scoped project after all upgrade changes are applied, then resolve the bounded set of compilation and compatibility issues in one pass.
- Do not generate a test baseline, deploy or delete Azure infrastructure, or create a pull request.

## User Preferences

### Technical Preferences
- Use the appropriate Azure SDK upgrade guidance and preserve application behavior.
- Scope changes to `storage-sample\Contoso.Documents.csproj` and directly related files only.
- Do not modify sibling sample projects.
- Follow the installed `migrating-azure-sdk-to-track2` skill and its Track 2 contract, package catalog, authentication, and behavior-audit references for blobs, queues, tables, and file shares.
- Verify API and default behavior against authoritative SDK sources rather than the sample README.
- Preserve queue message interoperability and refreshed pop receipts, existing connection-string/shared-key/SAS credential behavior, table ETag and 404 semantics, blob metadata and overwrite behavior, and Azure Files create-before-upload behavior.
- Preserve `BlobRepository.ListAsync` as block-blob-only across all asynchronous pages.
- Preserve the intended five-minute whole-operation blob deadline with operation-level cancellation; do not treat `Retry.NetworkTimeout` as equivalent to legacy `MaximumExecutionTime`.
- Update only `storage-sample\README.md` to describe the completed .NET 10 Track 2 state, verified WindowsAzure.Storage 9.3.3 Base64 behavior, public SDK type changes, `RenewLeaseAsync` receipt usage, absent tests, offline usage, and live-service limitations.

### Execution Style
- Do not deploy or delete infrastructure.
- Do not create a pull request in this run.
- Keep all validation offline; never use production credentials or contact live Azure.
- Remove `CONTOSO_STORAGE_CONNECTION` from any smoke-test process environment.
- No existing tests were found; do not report an empty `dotnet test` invocation as tests passing.
- If task lifecycle recovery remains blocked, use only the installed workflow's bounded artifact recovery/fallback and report the exact failure.

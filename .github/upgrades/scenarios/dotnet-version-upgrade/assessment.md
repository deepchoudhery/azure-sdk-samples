# .NET 10 Upgrade Assessment

## Scope

- **Project**: `Q:\azure-sdk-samples\storage-sample\Contoso.Documents.csproj`
- **Project type**: SDK-style console application
- **Target framework**: `net8.0` to `net10.0`
- **Source footprint**: 7 C# files, approximately 695 lines
- **Project references**: None
- **Test projects**: None

## Baseline and Compatibility

- The existing Release `net8.0` build succeeds with no warnings or errors.
- A Release `net10.0` compatibility build succeeds with no warnings or errors.
- Stable .NET 10 SDKs are installed, but the repository does not pin an SDK and currently selects a newer preview SDK. Pinning an appropriate stable .NET 10 SDK should be considered during execution.

## Dependency Assessment

The project depends on deprecated `WindowsAzure.Storage` 9.3.3. It spans four Azure Storage service areas and should be replaced with the current Azure SDK packages:

- `Azure.Storage.Blobs`
- `Azure.Storage.Queues`
- `Azure.Storage.Files.Shares`
- `Azure.Data.Tables`

The package migration is the primary source of upgrade complexity. Expected API changes include storage account and credential construction, retry configuration, blob uploads and listing, SAS generation, optimistic concurrency, queue encoding and message operations, table entities and queries, transactional batches, and Azure Files create-before-upload behavior.

## Security Findings

The legacy dependency graph includes three known high-severity transitive vulnerabilities:

| Package | Version | Advisory |
|---|---:|---|
| `Newtonsoft.Json` | 10.0.2 | `GHSA-5crp-9r3c-p9vr` |
| `System.Net.Http` | 4.3.0 | `GHSA-7jgj-8wvc-jh57` |
| `System.Text.RegularExpressions` | 4.3.0 | `GHSA-cmhx-cq75-c4mj` |

The project currently suppresses `NU1902` and `NU1903`. The upgrade must remove the vulnerable dependency chain and then remove those suppressions rather than carrying them forward.

## Risks and Validation Gaps

| Risk | Impact | Required validation |
|---|---|---|
| No automated tests | High | Add focused tests where practical and perform build plus sample-level behavioral validation |
| Queue encoding differences | High | Verify message payload round trips |
| Table missing-entity behavior | Medium | Preserve expected 404/not-found handling |
| SAS generation changes | High | Verify permissions, expiry, and resource scope |
| Retry behavior changes | Medium | Configure equivalent retry policies |
| Continuation and paging changes | Medium | Verify complete enumeration of results |
| Azure Files upload semantics | High | Preserve fixed-size file creation before range upload |
| Unpinned preview SDK selection | Medium | Pin or explicitly select a stable .NET 10 SDK |

## Recommended Upgrade Approach

Upgrade the target framework to `net10.0`, replace the monolithic legacy Storage SDK with the four service-specific Azure SDK packages, migrate each service area independently, remove vulnerability suppressions after the dependency graph is clean, and validate the complete project after each migration phase.

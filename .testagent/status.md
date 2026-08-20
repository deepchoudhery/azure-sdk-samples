# Storage Pre-Upgrade Regression Baseline — Final Status

## Outcome

- **Status:** SUCCESS
- **Step 8 coverage-gap review:** Complete; every explicit requirement and bounded public target has concrete final-test evidence.
- **Additional Step 8 test edits:** None. The completed quality gate had already closed all 17 verified gaps, so no optional breadth was added.
- **Discovery:** 41
- **Tests:** 41 passed, 0 failed, 0 skipped
- **Build:** 4/4 projects succeeded, 0 warnings, 0 errors
- **Formatting:** Passed
- **Live Azure/network use:** None
- **Production changes:** None

## Requirement Checklist Evidence

| # | Requirement | Final evidence |
|---:|---|---|
| 1 | Only `Contoso.Documents.csproj` scope | `Contoso.Documents.Tests.csproj` references only `storage-sample\Contoso.Documents.csproj`; the only test classes are `StorageAccountFactoryTests` and `QueueRepositoryTests`. |
| 2 | No production modifications | The final `git diff` and untracked-status check for `storage-sample` passed with no output or changes. |
| 3 | Create/register test project | `Contoso.Documents.Tests.sln` contains `storage-sample\Contoso.Documents.csproj` and `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`; root solution discovery found all 41 cases. |
| 4 | Deterministic/no live Azure | Storage tests only construct SDK objects. Queue tests use the nested `FakeCloudQueueClient` and `FakeCloudQueue`; all results and failures are in-memory completed tasks. |
| 5 | Exact `BlobEndpoint` `System.Uri` behavior | `Constructor_WithCanonicalConnectionString_ExposesCanonicalBlobEndpoint`, `Constructor_WithExplicitBlobEndpoint_PreservesEndpointText`, `TryCreate_WithCanonicalConnectionString_ReturnsFactoryAndExactEndpoint`, `FromSharedKey_WithFixedCredentials_UsesCanonicalHttpsEndpoint`, and `FromSasToken_WithFixedFakeToken_UsesCanonicalHttpsEndpoint` in `StorageAccountFactoryTests.cs` assert URI type, value, `OriginalString`, and `AbsoluteUri`. |
| 6 | Representative connection/account paths | The five tests above cover canonical/custom construction, valid parsing, shared key, and SAS. `Constructor_WithNullConnectionString_ThrowsArgumentNullException`, `Constructor_WithMalformedConnectionString_ThrowsFormatException`, and all three rows of `TryCreate_WithInvalidInput_ReturnsFalseAndNullFactory` cover null, empty, and malformed inputs. |
| 7 | Exponential retry/options | `CreateBlobClient_ConfiguresExecutionParallelismAndRetryPolicy` asserts five minutes, parallel count 4, endpoint, and `ExponentialRetry`; `CreateBlobClient_ExponentialRetryPinsFirstDelayAndAttemptBoundary` asserts the 3-second first delay and retry-count 3/4 boundary. |
| 8 | Linear retry | `CreateQueueClient_ConfiguresLinearRetry` asserts `LinearRetry`, the 2-second interval, and retry-count 2/3 boundary. |
| 9 | Queue delayed enqueue | `EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility` asserts Unicode string/UTF-8 bytes, exact seven-day TTL, caller-supplied 37-second delay, null options/context, and the selected overload. |
| 10 | Batch dequeue | `DequeueBatchAsync_WithEmptyQueue_ForwardsCountAndVisibility` asserts count, exact two-minute visibility, null options/context, empty output, and no deletes. `DequeueBatchAsync_WithTwoMessages_PreservesAndDeletesInOrder` asserts count, duration, payload order, and ordered `(Id, PopReceipt)` deletion. |
| 11 | Inspectable options/queue limitations | Retry behavior is checked through public `ShouldRetry`; queue forwarding is checked through virtual SDK overrides. No test claims wire-level serialization or service acceptance. |
| 12 | Other plausible regressions | Parsing/normalization: storage constructor and factory tests. UTF-8/null behavior: enqueue tests. Null semantics: empty dequeue/peek/count tests. Exact durations: delayed enqueue, batch dequeue, and renew lease tests. Ordering: two-message batch test. Error propagation: deterministic same-instance failure tests for every queue operation seam. |
| 13 | Exact commands/full workspace | Formatting, root solution discovery, fresh clean/test, and the prescribed recursive non-incremental build were run from the repository root; results are recorded below. |
| 14 | Final counts/unchanged production | Discovery found 41; fresh execution passed 41 with 0 failed/0 skipped; all four projects built with 0 warnings/0 errors; the production-tree Git check passed. |

## Bounded Target Inventory

### `storage-sample\StorageAccountFactory.cs`

Evidence file: `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs`

| Public target | Exact tests |
|---|---|
| Constructor / `BlobEndpoint` | `Constructor_WithCanonicalConnectionString_ExposesCanonicalBlobEndpoint`; `Constructor_WithExplicitBlobEndpoint_PreservesEndpointText`; `Constructor_WithNullConnectionString_ThrowsArgumentNullException`; `Constructor_WithMalformedConnectionString_ThrowsFormatException` |
| `TryCreate` | `TryCreate_WithCanonicalConnectionString_ReturnsFactoryAndExactEndpoint`; `TryCreate_WithInvalidInput_ReturnsFalseAndNullFactory` (3 discovered rows: null, empty, malformed) |
| `FromSharedKey` | `FromSharedKey_WithFixedCredentials_UsesCanonicalHttpsEndpoint` |
| `FromSasToken` | `FromSasToken_WithFixedFakeToken_UsesCanonicalHttpsEndpoint` |
| `CreateBlobClient` | `CreateBlobClient_ConfiguresExecutionParallelismAndRetryPolicy`; `CreateBlobClient_ExponentialRetryPinsFirstDelayAndAttemptBoundary` |
| `CreateQueueClient` | `CreateQueueClient_ConfiguresLinearRetry` |
| `CreateTableClient` | `CreateTableClient_ReturnsClientForFactoryAccount` |
| `CreateFileClient` | `CreateFileClient_ReturnsClientForFactoryAccount` |

### `storage-sample\QueueRepository.cs`

Evidence file: `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs`

| Public target | Exact tests |
|---|---|
| Constructor | `Constructor_WithQueueName_RequestsThatExactQueueReference` |
| `InitializeAsync` | `InitializeAsync_CreatesQueueIfMissingOnce`; `InitializeAsync_WhenSdkFails_PropagatesSameException` |
| `EnqueueAsync` | `EnqueueAsync_WithUnicodePayload_PreservesStringAndUtf8Bytes`; `EnqueueAsync_WithNullPayload_CreatesEmptyMessage`; `EnqueueAsync_WhenSdkFails_PropagatesSameException` |
| `EnqueueDelayedAsync` | `EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility`; `EnqueueDelayedAsync_WithNullPayload_CreatesEmptyMessage`; `EnqueueDelayedAsync_WhenSdkFails_PropagatesSameException` |
| `DequeueAsync` | `DequeueAsync_WhenQueueIsEmpty_ReturnsNullWithoutDelete`; `DequeueAsync_WhenRetrievalFails_PropagatesSameException`; `DequeueAsync_WithMessage_ReturnsPayloadAndDeletesIt`; `DequeueAsync_WhenDeleteFails_PropagatesSameException` |
| `DequeueBatchAsync` | `DequeueBatchAsync_WithEmptyQueue_ForwardsCountAndVisibility`; `DequeueBatchAsync_WithTwoMessages_PreservesAndDeletesInOrder`; `DequeueBatchAsync_WhenRetrievalFails_PropagatesAndDoesNotDelete`; `DequeueBatchAsync_WhenDeleteFails_PropagatesAndStopsProcessing` |
| `RenewLeaseAsync` | `RenewLeaseAsync_ForwardsMessageAndCallerVisibility`; `RenewLeaseAsync_WhenSdkFails_PropagatesSameException` |
| `PeekAsync` | `PeekAsync_WithMessage_ReturnsPayloadWithoutDelete`; `PeekAsync_WhenQueueIsEmpty_ReturnsNull`; `PeekAsync_WhenSdkFails_PropagatesSameException` |
| `GetApproximateLengthAsync` | `GetApproximateLengthAsync_WithNoServicePopulatedCount_FetchesAndReturnsDefault`; `GetApproximateLengthAsync_WhenSdkFails_PropagatesSameException` |
| `ClearAsync` | `ClearAsync_DelegatesOnce`; `ClearAsync_WhenSdkFails_PropagatesSameException` |

## Quality Findings and Fixes

The completed quality gate supplied for this final review reported:

- 17 verified coverage gaps fixed.
- 14 added cases:
  - `Constructor_WithNullConnectionString_ThrowsArgumentNullException`
  - `Constructor_WithMalformedConnectionString_ThrowsFormatException`
  - the null row of `TryCreate_WithInvalidInput_ReturnsFalseAndNullFactory`
  - `EnqueueAsync_WithNullPayload_CreatesEmptyMessage`
  - `EnqueueAsync_WhenSdkFails_PropagatesSameException`
  - `EnqueueDelayedAsync_WithNullPayload_CreatesEmptyMessage`
  - `EnqueueDelayedAsync_WhenSdkFails_PropagatesSameException`
  - `DequeueAsync_WhenRetrievalFails_PropagatesSameException`
  - `DequeueAsync_WhenDeleteFails_PropagatesSameException`
  - `DequeueBatchAsync_WhenDeleteFails_PropagatesAndStopsProcessing`
  - `RenewLeaseAsync_WhenSdkFails_PropagatesSameException`
  - `PeekAsync_WhenSdkFails_PropagatesSameException`
  - `GetApproximateLengthAsync_WhenSdkFails_PropagatesSameException`
  - `ClearAsync_WhenSdkFails_PropagatesSameException`
- Credential assertions were strengthened in `FromSharedKey_WithFixedCredentials_UsesCanonicalHttpsEndpoint` and `FromSasToken_WithFixedFakeToken_UsesCanonicalHttpsEndpoint`.
- Duration forwarding is pinned by `EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility`, both successful batch-dequeue tests, and `RenewLeaseAsync_ForwardsMessageAndCallerVisibility`.
- All 17 pseudo-mutations were caught.
- Assertion audit: no vacuous, trivial-only, assertion-free, or tautological tests.
- Step 8 found no remaining feasible requested gap, so the final test files were not edited and the quality gate did not need to be repeated.

## Final Commands and Results

Run from `Q:\azure-sdk-samples`:

| Purpose | Command | Result |
|---|---|---|
| SDK before | `dotnet --version` | `10.0.400-preview.0.26356.102` |
| Formatting | `dotnet format .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --verify-no-changes --no-restore` | Exit 0; no changes needed |
| Root discovery | `dotnet test .\Contoso.Documents.Tests.sln --list-tests --no-build --nologo` | Exit 0; 41 discovered; both test classes present |
| Fresh clean | `dotnet clean .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --nologo` | Exit 0; 0 warnings, 0 errors |
| Fresh tests | `dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --no-restore --nologo` | Exit 0; 41 passed, 0 failed, 0 skipped |
| Production diff | `git diff --exit-code -- storage-sample` | Exit 0 |
| Production status | `git status --porcelain --untracked-files=all -- storage-sample` | Empty; unchanged |
| SDK after | `dotnet --version` | `10.0.400-preview.0.26356.102` |

The full-workspace build enumerated non-output `*.csproj` files and ran
`dotnet build <project> --no-incremental --nologo` for each:

| Project | Result | Warnings | Errors |
|---|---|---:|---:|
| `keyvault-sample\Contoso.Secrets.csproj` | Succeeded | 0 | 0 |
| `servicebus-sample\Contoso.Ordering.csproj` | Succeeded | 0 | 0 |
| `storage-sample\Contoso.Documents.csproj` | Succeeded | 0 | 0 |
| `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj` | Succeeded | 0 | 0 |
| **Total** | **4/4 succeeded** | **0** | **0** |

`NETSDK1057` was emitted only as preview-SDK informational text, not as a warning or error.

## Testability Limitations

- The suite proves repository argument forwarding but not HTTP wire serialization or Azure service acceptance; those require an emulator, listener, or live service.
- Storage request-signing internals are package-private and exercised only during HTTP requests, so they remain outside this deterministic no-network baseline.
- `CloudQueue.ApproximateMessageCount` is service-populated and not publicly settable. The suite proves fetch invocation and null-to-zero behavior, but not a nonzero service-populated count.

## Files Changed

- `Contoso.Documents.Tests.sln` — added root test solution
- `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj` — added xUnit project
- `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs` — added 15 discovered cases
- `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs` — added 26 discovered cases
- `.testagent\research.md` — research artifact
- `.testagent\plan.md` — implementation plan
- `.testagent\status.md` — this final status

No file under `storage-sample` was changed. No production file was changed.

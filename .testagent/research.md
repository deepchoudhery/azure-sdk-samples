# Test Generation Research

## Project Overview
- **Path**: `Q:\azure-sdk-samples\storage-sample\Contoso.Documents.csproj`
- **Language**: C#
- **Framework**: .NET 8.0 executable (`net8.0`)
- **Test Framework**: None exists. Use xUnit (`xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 17.14.1); no mocking package is needed.
- **Project system**: SDK-style (`Microsoft.NET.Sdk`)
- **Dependency format and versions**: `PackageReference`; direct `WindowsAzure.Storage` 9.3.3, resolving its `netstandard1.3` asset and transitive `Newtonsoft.Json` 10.0.2.
- **New-file registration**: SDK projects use implicit `**/*.cs` compile globs. Put the test project at `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`, outside `storage-sample`, so production does not compile test files. Give it a `ProjectReference` to `..\..\storage-sample\Contoso.Documents.csproj`. Register both projects in a new root `Contoso.Documents.Tests.sln`; do not edit production.
- **SDK selection**: There is no `global.json`. The observed default SDK is `10.0.400-preview.0.26356.102`; SDK 8.0.424 is also installed. Record the SDK used for each before/after run.
- **Observed baseline**: `dotnet build .\storage-sample\Contoso.Documents.csproj --no-incremental --nologo` succeeded with **0 warnings and 0 errors**. The target's tracked files had no diff. The repository already had unrelated untracked `.github\` content, so whole-repository cleanliness must not be assumed.

## Dependency Graph
- **Interfaces/abstractions in scope**: None. Both target classes depend on concrete legacy SDK types.
- **Leaf types** (no in-scope dependencies): `StorageAccountFactory` (depends on `CloudStorageAccount`, clients, request options, credentials, and retry policies); `QueueRepository` (depends on `CloudQueueClient`, `CloudQueue`, and `CloudQueueMessage`).
- **Mid-layer types** (depend on leaves): None in the bounded target.
- **Top-layer types** (depend on mid-layer): None in the bounded target.
- **Test doubles**: `CloudQueueClient.GetQueueReference`, the relevant `CloudQueue.AddMessageAsync`/`GetMessagesAsync`, and delete methods are public virtual members. A handwritten fake client returning a recording fake queue can test queue argument forwarding without reflection, production changes, or network traffic.

## Build & Test Commands
Run all commands from `Q:\azure-sdk-samples`.

- **Build (target plus proposed tests)**: `dotnet build .\Contoso.Documents.Tests.sln --no-incremental --nologo`
- **Test (scoped — fix cycles)**: `dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --nologo`
- **Test (harness-equivalent — discovery check)**: `dotnet test --list-tests --nologo`
  - The no-target command works from the repository root after the single root `Contoso.Documents.Tests.sln` is added. Confirm that both proposed test classes and every test case are listed.
- **Lint**: No lint command/configuration exists. Use `dotnet format .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --verify-no-changes --no-restore`.
- **Existing authoritative production build**: `.\build-all.ps1 -Restore` builds all three production samples, but it does not force non-incremental compilation and does not include the proposed tests.
- **Full-workspace final build (non-incremental, including the new test project)**:

  ```powershell
  $projects = Get-ChildItem -Path . -Recurse -Filter *.csproj -File |
      Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.vs)[\\/]' }
  $failed = $false
  foreach ($project in $projects) {
      dotnet build $project.FullName --no-incremental --nologo
      if ($LASTEXITCODE -ne 0) { $failed = $true }
  }
  if ($failed) { exit 1 }
  ```

- **Fresh final test run**:

  ```powershell
  dotnet clean .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --nologo
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --no-restore --nologo
  ```

- **Production-unchanged check**:

  ```powershell
  git diff --exit-code -- storage-sample
  if (git status --porcelain --untracked-files=all -- storage-sample) {
      throw 'Unexpected production-tree changes under storage-sample.'
  }
  ```

Record the warning/error summary from every final build invocation and the passed/failed/skipped test counts. Do not infer final counts from the current baseline.

## Scope
- **Boundary**: Regression tests only for `Contoso.Documents.csproj`, specifically the requested behavior in `StorageAccountFactory.cs` and `QueueRepository.cs`. No sibling project or unrelated production source inventory.
- **Targets**:
  - `storage-sample\StorageAccountFactory.cs`
  - `storage-sample\QueueRepository.cs`
- **Source-to-test pairs**:
  - `StorageAccountFactory.cs` → `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs`
  - `QueueRepository.cs` → `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs`
- **Representative existing tests**: None found in the bounded project.

## Files to Test

### High Priority
| File | Classes/Functions | Testability | Estimated Coverage | Notes |
|------|-------------------|-------------|-------------------|-------|
| `storage-sample\StorageAccountFactory.cs` | Constructor, `TryCreate`, `FromSharedKey`, `FromSasToken`, `CreateBlobClient`, `CreateQueueClient`, `CreateTableClient`, `CreateFileClient`, `BlobEndpoint` | High | Untested | All priority behavior is publicly inspectable and constructs clients without I/O. |
| `storage-sample\QueueRepository.cs` | Constructor, `InitializeAsync`, `EnqueueAsync`, `EnqueueDelayedAsync`, `DequeueAsync`, `DequeueBatchAsync`, `RenewLeaseAsync`, `PeekAsync`, `GetApproximateLengthAsync`, `ClearAsync` | High for argument forwarding with fakes | Untested | Required TTL/visibility behavior is testable through virtual SDK methods without Azure. |

### Medium Priority
No additional source files are in the requested target inventory. Secondary cases within the two files are listed below.

### Low Priority / Skip
| File | Reason |
|------|--------|
| Other production files | Outside the requested regression scope; intentionally not inventoried. |

## Existing Tests & Coverage Classification
- `StorageAccountFactory.cs`: **untested** — no paired test file or test project exists.
- `QueueRepository.cs`: **untested** — no paired test file or test project exists.
- No numeric coverage estimate is available, and coverage collection was not requested.

## Existing Test Projects
None found for `Contoso.Documents.csproj`; there is also no existing `.sln` or `.slnx`.

Recommended new project:
- **Project file**: `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`
- **Target source project**: `storage-sample\Contoso.Documents.csproj`
- **Test files**: `StorageAccountFactoryTests.cs`, `QueueRepositoryTests.cs`
- **Registration**: root `Contoso.Documents.Tests.sln` containing only the target production project and its test project. This is the minimal authoritative test harness and enables root-level no-target discovery.

## Testing Patterns
- Use xUnit `[Fact]`/`[Theory]`, Arrange/Act/Assert, fixed account names, a fixed generated Base64 key, and fake SAS text. Never read credentials or call Azure.
- Use exact `TimeSpan` and `Uri` assertions. For URI regressions, assert the value is a `System.Uri`, compare the `Uri`, and compare `OriginalString`/`AbsoluteUri` where trailing-slash preservation matters.
- Use only public behavior for retry verification: cast the configured retry policy and call `ShouldRetry` with HTTP 500, a non-null exception, and a new `OperationContext`.
- Define recording `FakeCloudQueueClient`/`FakeCloudQueue` types inside `QueueRepositoryTests.cs`. Return completed tasks or in-memory message sequences. Do not use reflection, global SDK events, Azurite, sockets, or live endpoints.
- Keep tests independent and parallel-safe; no current-culture or global-state mutation is needed.

### Required test cases

1. **`BlobEndpoint` and construction paths**
   - Public constructor with canonical connection string → exact `System.Uri` `https://contosodocs.blob.core.windows.net/`.
   - Public constructor with explicit `BlobEndpoint=http://127.0.0.1:10000/contosodocs` → preserve that exact URI (including its no-trailing-slash `OriginalString`).
   - `TryCreate` with a valid canonical string → `true`, non-null factory, same exact endpoint.
   - `TryCreate` with malformed/empty input → `false` and null factory.
   - `FromSharedKey("contosodocs", fixedBase64Key)` → exact HTTPS default blob endpoint.
   - `FromSasToken("contosodocs", fixedFakeSas)` → exact HTTPS default blob endpoint.
   - These directly pin the assessed `public Uri BlobEndpoint { get { return _account.BlobEndpoint; } }` behavior at delivered lines 106-108 (assessment lines 105-107), with no network operation.

2. **Blob request options**
   - `CreateBlobClient().DefaultRequestOptions` is non-null.
   - `MaximumExecutionTime == TimeSpan.FromMinutes(5)`.
   - `ParallelOperationThreadCount == 4`.
   - Retry policy is `ExponentialRetry`.
   - Its public `ShouldRetry` behavior returns `true` at retry count 0 with an exact first interval of 3 seconds; count 3 is allowed and count 4 is rejected, pinning four maximum retry attempts. Do not assert later randomized exponential intervals.

3. **Queue request options**
   - `CreateQueueClient().DefaultRequestOptions.RetryPolicy` is `LinearRetry`.
   - Public `ShouldRetry` returns an exact 2-second interval; count 2 is allowed and count 3 is rejected, pinning three maximum retry attempts.

4. **Delayed enqueue**
   - Call `EnqueueDelayedAsync` with a distinctive delay such as 37 seconds.
   - The recording queue must receive the original Unicode payload, TTL exactly 7 days, visibility exactly the supplied delay, and null request options/context. Also assert the captured legacy `CloudQueueMessage.AsBytes` equals UTF-8 bytes to pin current message construction/encoding.

5. **Batch dequeue**
   - The recording queue must receive the caller's count and visibility timeout exactly 2 minutes, with null request options/context.
   - Return an empty sequence for the smallest argument-only test.
   - Secondary test: return two in-memory messages and verify payload order is retained and `(Id, PopReceipt)` deletions occur in the same order.

6. **Secondary public-contract cases**
   - `DequeueAsync` returns null and performs no delete when the fake queue returns no message.
   - A Unicode payload survives `EnqueueAsync` as the same string/UTF-8 bytes.
   - These are cheap guards for null semantics and package/message encoding.

## Other Upgrade-Sensitive Behavior in Scope
- **Package behavior**: The old 9.3.3 package contributes a `netstandard1.3` binary and old Newtonsoft.Json 10.0.2. Connection-string parsing, endpoint synthesis, and retry-policy behavior are the useful deterministic compatibility seams.
- **Serialization/encoding**: Queue payload construction uses `CloudQueueMessage(string)` and currently exposes UTF-8 bytes. Pin a non-ASCII sample. This does not claim to test wire encoding.
- **Culture/formatting**: Target code performs no culture-sensitive formatting. URI and `TimeSpan` assertions are culture-independent; do not mutate process culture merely to manufacture coverage.
- **Date/time**: Test exact durations only. No wall clock, time zone, expiration timestamp, or delay should be awaited.
- **Ordering**: `DequeueBatchAsync` preserves SDK enumeration order and deletes sequentially; the in-memory two-message case can pin this.
- **Hashing/signing**: Storage request signing is package-internal and only relevant during HTTP requests; exclude it from this no-network suite.
- **Regex and file I/O**: None exists in the two target files.
- **Concurrency**: No parallel work is initiated in `QueueRepository`; `ParallelOperationThreadCount = 4` is configuration and should be asserted directly. Avoid timing-based concurrency tests.

## Queue Feasibility and Limitation
The required queue TTL and visibility checks are feasible without production changes because the concrete legacy SDK methods are virtual. The tests will prove that `QueueRepository` passes 7 days, the caller's delay, and 2 minutes to the correct overloads. They will not prove HTTP query serialization or Azure service acceptance. Testing that lower layer would require a local HTTP server/emulator or brittle internals and is intentionally excluded.

## Requirement Checklist
1. **Only `Contoso.Documents.csproj` scope** — bounded to its two requested target files.
2. **No production modifications** — tests live outside `storage-sample`; final diff/status checks enforce this.
3. **Create/register test project** — create `tests\Contoso.Documents.Tests`, reference production, and register both in root `Contoso.Documents.Tests.sln`.
4. **Deterministic/no live Azure** — construction, public options, and recording virtual fakes only.
5. **Exact `BlobEndpoint` `System.Uri` behavior** — exact type/value/string assertions across all public construction paths.
6. **Representative connection/account paths** — constructor canonical/custom endpoint, `TryCreate`, shared key, and SAS.
7. **Exponential retry/options** — 3-second first backoff, four-attempt boundary, 5-minute maximum execution, parallel count 4.
8. **Linear retry** — exact 2-second interval and three-attempt boundary.
9. **Queue delayed enqueue** — exact 7-day TTL and caller-supplied visibility delay.
10. **Batch dequeue** — exact 2-minute visibility timeout.
11. **Inspectable options/queue limitations** — public request options and virtual fake seam prioritized; no wire-level claim.
12. **Other plausible regressions** — package parsing, URI normalization, UTF-8 message construction, null behavior, duration handling, and ordering; absent categories documented.
13. **Exact commands/full workspace** — scoped build/test, no-target discovery, lint, non-incremental all-project build, and fresh final test commands are above.
14. **Final counts/unchanged production** — report actual final warning/error and test counts; require the production-tree diff/status checks above. Current scoped baseline is 0 warnings/0 errors.

## Recommendations
1. Add the external test project and root solution only; do not place tests below `storage-sample` because its implicit compile glob would pull them into production.
2. Implement `StorageAccountFactoryTests` first: endpoint paths, parse failure, blob options, then queue retry options.
3. Implement `QueueRepositoryTests` with handwritten recording fakes: delayed enqueue and batch timeout first, then encoding/null/order cases.
4. Run the scoped test command during fixes, the root no-target discovery command before completion, then the fresh final test and non-incremental full-workspace build. Report exact summaries.
5. Run the production-unchanged checks. Allow only the proposed root solution and `tests\Contoso.Documents.Tests` additions; do not alter the pre-existing unrelated `.github\` state.

# Test Implementation Plan

## Overview

Create a pre-upgrade regression baseline for the two untested public types in `storage-sample\Contoso.Documents.csproj`. Because no test project or solution exists, first add a minimal external xUnit project and root solution, then test the leaf types in increasing complexity: deterministic storage-account/client construction followed by queue orchestration through handwritten virtual fakes. No production file will be modified, and no test will contact Azure, Azurite, a socket, or the clock.

This is a broad strategy for the complete bounded inventory. Each production target is assigned to exactly one implementation phase:

- `storage-sample\StorageAccountFactory.cs` → Phase 2
- `storage-sample\QueueRepository.cs` → Phase 3

## Commands

Run all commands from `Q:\azure-sdk-samples`.

- **Record SDK before and after validation**: `dotnet --version`
- **Restore/build the new harness**: `dotnet build .\Contoso.Documents.Tests.sln --no-incremental --nologo`
- **Scoped test/fix cycle**: `dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --nologo`
- **Root discovery check**: `dotnet test --list-tests --nologo`
- **Lint/format verification**: `dotnet format .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --verify-no-changes --no-restore`
- **Production-only reference build**: `dotnet build .\storage-sample\Contoso.Documents.csproj --no-incremental --nologo`
- **Authoritative production script, when desired**: `.\build-all.ps1 -Restore`

The root discovery command relies on `Contoso.Documents.Tests.sln` being the sole root solution. It must list both `StorageAccountFactoryTests` and `QueueRepositoryTests`, including every `[Theory]` row.

### Full-workspace final build

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

### Fresh final test run

```powershell
dotnet clean .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj --no-restore --nologo
```

### Production-unchanged check

```powershell
git diff --exit-code -- storage-sample
if (git status --porcelain --untracked-files=all -- storage-sample) {
    throw 'Unexpected production-tree changes under storage-sample.'
}
```

Record the SDK version, each final build's warning/error summary, final passed/failed/skipped counts, and discovery count. Do not substitute the observed production baseline of 0 warnings/0 errors for actual final results, and do not treat unrelated pre-existing untracked `.github\` content as a failure.

## Phase Summary

| Phase | Focus | Files | Est. Tests |
|---|---|---:|---:|
| 1 | Minimal test harness and registration | 2 new infrastructure files | 0 |
| 2 | Storage account, endpoint, client-option, and retry contracts | 1 target / 1 test file | 11-12 |
| 3 | Queue forwarding, encoding, null, duration, and ordering contracts | 1 target / 1 test file | 14-16 |
| 4 | Discovery and pre-upgrade baseline validation | No new files | 25-28 total |

---

## Phase 1: Minimal Test Harness

### Overview

Establish the smallest authoritative harness outside `storage-sample` so SDK compile globs cannot pull tests into production. Pin the researched package versions and register only the target production project and its tests.

### Files to Create

#### 1. `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`

- **Project SDK**: `Microsoft.NET.Sdk`
- **Target framework**: `net8.0`
- **Test settings**: `IsTestProject=true`, `IsPackable=false`; enable implicit usings and nullable annotations for new test code.
- **Package references**:
  - `Microsoft.NET.Test.Sdk` `17.14.1`
  - `xunit` `2.9.3`
  - `xunit.runner.visualstudio` `3.1.5`, private assets enabled
- **Project reference**: `..\..\storage-sample\Contoso.Documents.csproj`
- Do not add a mocking library, Azure emulator dependency, generated placeholder test, or production link.

#### 2. `Contoso.Documents.Tests.sln`

- Create an actual `.sln` (with the installed SDK, use `dotnet new sln --name Contoso.Documents.Tests --format sln` if generating it).
- Register:
  - `storage-sample\Contoso.Documents.csproj`
  - `tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj`
- Registration command:

  ```powershell
  dotnet sln .\Contoso.Documents.Tests.sln add `
      .\storage-sample\Contoso.Documents.csproj `
      .\tests\Contoso.Documents.Tests\Contoso.Documents.Tests.csproj
  ```

- Verify registration with `dotnet sln .\Contoso.Documents.Tests.sln list`.

### Success Criteria

- [ ] Only the root solution and external test-project tree are added.
- [ ] The solution lists exactly the production target and test project.
- [ ] `dotnet build .\Contoso.Documents.Tests.sln --no-incremental --nologo` succeeds.
- [ ] The production-unchanged check succeeds.

---

## Phase 2: Storage Account and Client Configuration Contracts

### Overview

Test the simpler leaf first. These tests pin deterministic behavior exposed by the legacy storage package without making a request: connection parsing, endpoint synthesis/preservation, client creation, request options, and public retry decisions.

### Files to Test

#### 1. `StorageAccountFactory.cs`

- **Source**: `storage-sample\StorageAccountFactory.cs`
- **Coverage classification**: Untested
- **Test File**: `tests\Contoso.Documents.Tests\StorageAccountFactoryTests.cs`
- **Test Class**: `StorageAccountFactoryTests`
- **Style**: xUnit `[Fact]`/`[Theory]`, Arrange/Act/Assert
- **Fixed data**:
  - Account name: `contosodocs`
  - Base64 key: a checked-in non-secret constant representing fixed bytes, such as `MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=`
  - Canonical connection string assembled from that account/key and `DefaultEndpointsProtocol=https`
  - A fixed valid-shape fake SAS token with a far-future literal expiry; never use it for a request

**Public members and concrete test cases**:

1. **Constructor and `BlobEndpoint`**
   - `Constructor_WithCanonicalConnectionString_ExposesCanonicalBlobEndpoint`
     - Construct with the canonical string.
     - Assert `BlobEndpoint` is a `System.Uri`.
     - Assert exact value, `OriginalString`, and `AbsoluteUri` are `https://contosodocs.blob.core.windows.net/`.
   - `Constructor_WithExplicitBlobEndpoint_PreservesEndpointText`
     - Use `BlobEndpoint=http://127.0.0.1:10000/contosodocs`.
     - Assert exact `Uri` value and exact no-trailing-slash `OriginalString`; assert the expected `AbsoluteUri`.

2. **`TryCreate`**
   - `TryCreate_WithCanonicalConnectionString_ReturnsFactoryAndExactEndpoint`
     - Assert `true`, non-null factory, and the same canonical exact `System.Uri` checks.
   - `TryCreate_WithInvalidInput_ReturnsFalseAndNullFactory`
     - Use a `[Theory]` with `""` and a fixed malformed string.
     - Assert `false` and a null out value for both; no environment credentials are read.

3. **`FromSharedKey`**
   - `FromSharedKey_WithFixedCredentials_UsesCanonicalHttpsEndpoint`
     - Assert a non-null factory and exact `https://contosodocs.blob.core.windows.net/` URI/string values.

4. **`FromSasToken`**
   - `FromSasToken_WithFixedFakeToken_UsesCanonicalHttpsEndpoint`
     - Assert a non-null factory and the same exact URI/string values without performing I/O.

5. **`CreateBlobClient`**
   - `CreateBlobClient_ConfiguresExecutionParallelismAndRetryPolicy`
     - Assert the client and `DefaultRequestOptions` are non-null.
     - Assert `MaximumExecutionTime == TimeSpan.FromMinutes(5)` and `ParallelOperationThreadCount == 4`.
     - Assert `RetryPolicy` is `ExponentialRetry`.
   - `CreateBlobClient_ExponentialRetryPinsFirstDelayAndAttemptBoundary`
     - Call public `ShouldRetry` with HTTP 500, a fixed non-null exception, and a new `OperationContext`.
     - At count 0 assert `true` and exactly 3 seconds.
     - At count 3 assert `true`; at count 4 assert `false`.
     - Do not assert randomized later intervals.

6. **`CreateQueueClient`**
   - `CreateQueueClient_ConfiguresLinearRetry`
     - Assert the client/default options are non-null and the policy is `LinearRetry`.
     - Through public `ShouldRetry`, assert an exact 2-second interval, count 2 allowed, and count 3 rejected using HTTP 500, a fixed exception, and a fresh `OperationContext`.

7. **`CreateTableClient`**
   - `CreateTableClient_ReturnsClientForFactoryAccount`
     - Assert a non-null client with exact `https://contosodocs.table.core.windows.net/` base URI. This is construction-only and must not query tables.

8. **`CreateFileClient`**
   - `CreateFileClient_ReturnsClientForFactoryAccount`
     - Assert a non-null client with exact `https://contosodocs.file.core.windows.net/` base URI. This is construction-only and must not query files.

### Success Criteria

- [ ] Every public constructor, factory method, client-creation method, and `BlobEndpoint` has direct coverage.
- [ ] Exact URI type/value/string, option durations/counts, and retry boundaries are pinned.
- [ ] Tests contain only fixed fake credentials and perform no network operation.
- [ ] Scoped build and test commands pass before Phase 3.

---

## Phase 3: Queue Repository Public Behavior

### Overview

Test the second leaf through public virtual legacy-SDK seams. Define recording fakes as private nested types in `QueueRepositoryTests.cs`; return completed tasks and in-memory messages only. Keep all state per test so xUnit parallel execution remains safe.

### Files to Test

#### 1. `QueueRepository.cs`

- **Source**: `storage-sample\QueueRepository.cs`
- **Coverage classification**: Untested
- **Test File**: `tests\Contoso.Documents.Tests\QueueRepositoryTests.cs`
- **Test Class**: `QueueRepositoryTests`

**Fake design**:

- `FakeCloudQueueClient` overrides `GetQueueReference` to record the requested queue name and return one injected `FakeCloudQueue`.
- `FakeCloudQueue` records calls/arguments for `CreateIfNotExistsAsync`, both relevant `AddMessageAsync` forms, `GetMessageAsync`, `GetMessagesAsync`, applicable delete overloads, `UpdateMessageAsync`, `PeekMessageAsync`, `FetchAttributesAsync`, and `ClearAsync`.
- Provide configurable returned messages and a configurable fixed exception. Never use reflection, global SDK events, Azurite, sockets, or live endpoints.
- Build in-memory messages with fixed payloads, IDs, pop receipts, and literal timestamps through public SDK construction APIs. Do not use `DateTimeOffset.Now`.

**Public members and concrete test cases**:

1. **Constructor**
   - `Constructor_WithQueueName_RequestsThatExactQueueReference`
     - Construct the repository with the fake client and a distinctive queue name.
     - Assert one exact `GetQueueReference` call and that a subsequent operation targets the injected queue.

2. **`InitializeAsync`**
   - `InitializeAsync_CreatesQueueIfMissingOnce`
     - Assert one `CreateIfNotExistsAsync` call and successful task completion.
   - `InitializeAsync_WhenSdkFails_PropagatesSameException`
     - Configure a fixed fake exception and assert the same exception instance reaches the caller.

3. **`EnqueueAsync`**
   - `EnqueueAsync_WithUnicodePayload_PreservesStringAndUtf8Bytes`
     - Use a non-ASCII fixed payload.
     - Assert one added `CloudQueueMessage`, exact `AsString`, and `AsBytes` equal `Encoding.UTF8.GetBytes(payload)`.

4. **`EnqueueDelayedAsync`**
   - `EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility`
     - Use the same Unicode payload and `TimeSpan.FromSeconds(37)`.
     - Assert exact payload/string and UTF-8 bytes, TTL `TimeSpan.FromDays(7)`, visibility delay 37 seconds, and null request options/context.
     - Assert the fake returns immediately; never wait for the delay.

5. **`DequeueAsync`**
   - `DequeueAsync_WhenQueueIsEmpty_ReturnsNullWithoutDelete`
     - Return no message; assert null and zero delete calls.
   - `DequeueAsync_WithMessage_ReturnsPayloadAndDeletesIt`
     - Return one fixed in-memory message; assert exact payload and one deletion using that message's exact identity/pop receipt.

6. **`DequeueBatchAsync`**
   - `DequeueBatchAsync_WithEmptyQueue_ForwardsCountAndVisibility`
     - Use the smallest valid argument-only count and return an empty sequence.
     - Assert exact caller count, visibility timeout `TimeSpan.FromMinutes(2)`, null request options/context, an empty result, and no deletes.
   - `DequeueBatchAsync_WithTwoMessages_PreservesAndDeletesInOrder`
     - Return two fixed messages with distinct Unicode payloads, IDs, and pop receipts.
     - Materialize the returned sequence and assert payload order.
     - Assert `(Id, PopReceipt)` deletions occur exactly once each and in source enumeration order.
   - `DequeueBatchAsync_WhenRetrievalFails_PropagatesAndDoesNotDelete`
     - Throw a fixed exception from the fake retrieval; assert the same exception and no delete calls.

7. **`RenewLeaseAsync`**
   - `RenewLeaseAsync_ForwardsMessageAndTwoMinuteVisibility`
     - Pass a fixed message.
     - Assert the same message is updated with `TimeSpan.FromMinutes(2)` and `MessageUpdateFields.Visibility`; record and assert null options/context if the production overload exposes them.

8. **`PeekAsync`**
   - `PeekAsync_WithMessage_ReturnsPayloadWithoutDelete`
     - Return one fixed Unicode message; assert exact payload and no update/delete.
   - `PeekAsync_WhenQueueIsEmpty_ReturnsNull`
     - Return no message; assert null and no destructive calls.

9. **`GetApproximateLengthAsync`**
   - `GetApproximateLengthAsync_WithNoServicePopulatedCount_FetchesAndReturnsDefault`
     - Complete `FetchAttributesAsync`, assert exactly one fetch, and assert the public default result is `0` when `ApproximateMessageCount` remains null.
   - Do not use reflection to force a non-zero service-populated count.

10. **`ClearAsync`**
    - `ClearAsync_DelegatesOnce`
      - Assert exactly one `ClearAsync` call and successful completion.

### Queue Limitations

The virtual fake seam can prove repository-level forwarding of the seven-day TTL, caller delay, two-minute dequeue visibility, message update visibility, null options/context, payload encoding, and delete order without production changes.

It cannot prove that `WindowsAzure.Storage` serializes those values into the expected HTTP query, that Azure accepts them, or that service-generated IDs/pop receipts behave identically. Those checks require a live service, emulator/local HTTP fixture, or brittle package internals and are excluded.

`CloudQueue.ApproximateMessageCount` is service-populated and not publicly settable. The deterministic no-network suite can prove the fetch call and null-to-zero behavior, but not a non-zero fetched count. A deterministic non-zero unit test would require a new production abstraction/injection seam (or prohibited reflection/networking), so it is explicitly deferred rather than modifying production.

### Success Criteria

- [ ] Every listed public `QueueRepository` method has direct public-behavior coverage.
- [ ] Required TTL, delays, null arguments, encoding, null semantics, and ordering are asserted exactly.
- [ ] All fake state is per test and all completions are deterministic.
- [ ] No test starts Azure, Azurite, a listener, a timer wait, or a wall-clock dependency.
- [ ] Scoped build and test commands pass.

---

## Phase 4: Discovery and Baseline Validation

### Overview

Run the completed suite from clean state, prove root-level discovery, check formatting and all projects, and record the actual baseline. This phase adds no tests or production changes.

### Execution Order

1. Run `dotnet --version` and record the selected SDK.
2. Run the lint command.
3. Run `dotnet test --list-tests --nologo`; verify both classes and every fact/theory row are listed.
4. Run the fresh final test commands and record passed/failed/skipped counts.
5. Run the full-workspace non-incremental build and record warning/error summaries for every project.
6. Run the production-unchanged check.
7. Run `dotnet --version` again and record it; the before/after values must agree or the SDK change must be reported.

### Success Criteria

- [ ] Formatting verification succeeds with no restore.
- [ ] Root no-target discovery finds every intended test case.
- [ ] Fresh tests report zero failed tests.
- [ ] Every workspace project builds successfully; actual warning/error totals are recorded.
- [ ] No tracked or untracked change exists under `storage-sample`.
- [ ] Only `Contoso.Documents.Tests.sln` and `tests\Contoso.Documents.Tests\...` are intentional additions.

---

## Requirement Traceability

| # | Preserved requirement | Plan mapping |
|---:|---|---|
| 1 | Only `Contoso.Documents.csproj` scope | Overview and Phases 2-3 assign only `StorageAccountFactory.cs` and `QueueRepository.cs`; sibling projects receive no tests. |
| 2 | No production modifications | Phase 1 locates tests externally; production-unchanged command and Phase 4 gate enforce it. |
| 3 | Create/register test project | Phase 1 specifies the exact project, pinned packages, project reference, root `.sln`, and registration/list commands. |
| 4 | Deterministic/no live Azure | Fixed fixtures and construction-only assertions in Phase 2; in-memory recording fakes and explicit network/timing prohibitions in Phase 3. |
| 5 | Exact `BlobEndpoint` `System.Uri` behavior | Phase 2 constructor, explicit endpoint, `TryCreate`, shared-key, and SAS cases assert type, value, `OriginalString`, and `AbsoluteUri`. |
| 6 | Representative connection/account paths | Phase 2 covers canonical/custom constructors, valid/invalid `TryCreate`, shared key, and SAS. |
| 7 | Exponential retry/options | Phase 2 asserts 3-second first backoff, counts 3/4 boundary, five-minute maximum, and parallel count 4. |
| 8 | Linear retry | Phase 2 asserts exact two-second interval and counts 2/3 boundary. |
| 9 | Queue delayed enqueue | Phase 3 asserts exact seven-day TTL, 37-second caller delay, null options/context, and Unicode bytes. |
| 10 | Batch dequeue | Phase 3 asserts exact two-minute visibility, count forwarding, empty behavior, payload/deletion order. |
| 11 | Inspectable options/queue limitations | Phase 2 uses public options/`ShouldRetry`; Phase 3 uses virtual fakes and separately documents wire/service limits. |
| 12 | Other plausible regressions | Phases 2-3 cover package parsing, URI normalization, UTF-8 construction, null behavior, exact durations, and ordering. Hashing/signing and wire encoding require HTTP and are excluded; regex/file I/O are absent; culture mutation is unnecessary; no wall-clock/concurrency timing test is planned. |
| 13 | Exact commands/full workspace | Commands and Phase 4 include scoped build/test, root discovery, lint, fresh test, and non-incremental all-project build verbatim. |
| 14 | Final counts/unchanged production | Commands and Phase 4 require actual SDK/build/test summaries plus exact production diff/status checks; no count is inferred from the current baseline. |


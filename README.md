# Azure SDK Migration — Skill Test Samples

Three deliberately **legacy** .NET applications used to test the `migrating-azure-*`
lazy skills shipped by the modernization agent.

Each sample is a self-contained git repository in the "before" state: it compiles,
it references a deprecated Azure SDK, and it exercises a broad slice of that SDK's
API surface so the corresponding skill has something meaningful to convert.

| Sample | Deprecated package(s) | TFM | Skill under test |
|--------|----------------------|-----|------------------|
| [`keyvault-sample`](keyvault-sample) | `Microsoft.Azure.KeyVault`, `Microsoft.Azure.KeyVault.WebKey`, `Microsoft.Azure.Services.AppAuthentication` | `net8.0` | `migrating-azure-keyvault` |
| [`servicebus-sample`](servicebus-sample) | `WindowsAzure.ServiceBus` | `net472` | `migrating-azure-servicebus` |
| [`storage-sample`](storage-sample) | `WindowsAzure.Storage` | `net8.0` | `migrating-azure-storage` |

`servicebus-sample` targets `net472` because `Microsoft.ServiceBus.dll` only ships a
`net462` asset — there is no way to build it on .NET Core. The other two legacy
packages ship `netstandard` assets, so they build on `net8.0`.

## Establishing a baseline

```powershell
.\build-all.ps1
```

All three projects must build before you start. Warnings about deprecated packages
(`NU1903`, `NU1902`, `SYSLIB0051`, ...) are expected and are part of the point — they
are the signal the skills key off.

## Running a skill against a sample

Each sample is its own git repo, so the agent sees a clean tree and you can diff
precisely what the skill changed.

```powershell
cd Q:\azure-sdk-samples\keyvault-sample
copilot            # then ask it to migrate off the deprecated Key Vault SDK
```

After the run:

```powershell
git -C Q:\azure-sdk-samples\keyvault-sample diff
dotnet build Q:\azure-sdk-samples\keyvault-sample
```

## Grading the result

Every sample has a `README.md` with an **Expected migration outcome** checklist derived
directly from its skill's mapping tables. Walk that checklist against the diff — it is
the scoring rubric. A migration is only correct if:

1. Every deprecated package reference is gone from the `.csproj`.
2. Every call site in the table has been converted (not just the ones in `Program.cs`).
3. `dotnet build` succeeds with no unresolved types.
4. Behavior is preserved — pagination still paginates, error handling still catches the
   same conditions, message settlement still settles.

## Resetting between runs

```powershell
.\reset-all.ps1
```

This hard-resets each sample repo back to its committed baseline.

## Note on credentials

Nothing here talks to Azure. Connection strings and vault URIs come from environment
variables and fall back to obviously-fake placeholder values, and `Program.cs` in each
sample refuses to run any network operation unless the relevant environment variable is
set. The samples are compile-time fixtures, not runnable demos.

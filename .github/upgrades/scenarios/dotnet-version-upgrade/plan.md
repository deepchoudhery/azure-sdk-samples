# .NET 10 Upgrade Plan

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: The confirmed scope contains one SDK-style console application targeting .NET 8 with no project dependencies, no incompatible packages, and one assessed behavioral API issue, so a single atomic upgrade avoids unnecessary phasing.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | A single modern .NET project with no dependency graph is best upgraded and validated as one bounded operation. |
| Test Coverage | Skip | Do not generate new coverage; retain the existing scoped build and test validation. |

## Project Group

- Application: `keyvault-sample\Contoso.Secrets.csproj` (`net8.0` → `net10.0`)
- Scope is limited to this project and directly relevant files under `keyvault-sample`; sibling projects are excluded.

## Tasks

### 01-upgrade-keyvault-sample: Upgrade the Contoso Key Vault sample to .NET 10 and the current Azure SDK

Verify the .NET 10 SDK and any applicable `global.json` constraints, then upgrade `keyvault-sample\Contoso.Secrets.csproj` from `net8.0` to `net10.0`. In the same atomic task, replace the retired `Microsoft.Azure.KeyVault`, `Microsoft.Azure.KeyVault.WebKey`, and `Microsoft.Azure.Services.AppAuthentication` packages with the directly required `Azure.Security.KeyVault.Secrets`, `Azure.Security.KeyVault.Keys`, `Azure.Security.KeyVault.Certificates`, and `Azure.Identity` packages, removing obsolete vulnerability suppressions when the legacy dependencies are gone.

Preserve the sample's authentication, secret, key, certificate, paging, error-handling, cross-vault copy, cryptography, and correlation-ID behavior while moving the directly relevant code to current Azure SDK client and credential patterns. Research should begin with `VaultClientFactory.cs`, `CorrelationIdHandler.cs`, `SecretManager.cs`, `KeyManager.cs`, `CertificateManager.cs`, `TokenPayloadReader.cs`, `Program.cs`, and `README.md`; retain user-assigned identity pinning, use a destination-vault client for secret copies, preserve all four not-found paths, and account for the assessed `HttpContent.ReadAsStringAsync` behavioral change. Do not deploy or delete Azure infrastructure, modify sibling projects, generate new test coverage, or create a pull request.

After the atomic code and package migration, restore and build the scoped project and run any existing tests applicable to it. Fix all compilation errors and warnings introduced or touched by the upgrade, and verify that no legacy Key Vault SDK or AppAuthentication references remain.

**Done when**: `Contoso.Secrets.csproj` targets `net10.0`; only the required modern Azure Key Vault and identity packages remain; the directly relevant source and documentation preserve the documented sample behavior; scoped restore/build and existing tests pass; searches find no surviving `Microsoft.Azure.KeyVault`, `Microsoft.Azure.Services.AppAuthentication`, `KeyVaultClient`, `SecretBundle`, `KeyBundle`, or `CertificateBundle` usage; and no infrastructure or sibling-project changes were made.

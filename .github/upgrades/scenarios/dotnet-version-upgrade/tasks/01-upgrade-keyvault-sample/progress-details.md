# Progress details — 01-upgrade-keyvault-sample

## Status

Implementation complete. Scoped restore, build, offline smoke test, legacy-reference search,
package-graph inspection, vulnerability inspection, and test discovery completed. Live Azure
service semantics remain **Unchecked** because this task prohibited production credentials and
live Azure access.

## Research and package resolution

- Read the complete `migrating-azure-sdk-to-track2` skill and the applicable
  `track2-contract.md`, `package-catalog.md`, `authentication.md`, and
  `behavior-audit.md` references. `management-plane.md` was intentionally not used because
  Secrets, Keys, and Certificates are Key Vault data-plane clients.
- Read the complete `managing-target-frameworks`, `managing-package-references`, and
  `building-projects` skills.
- Azure's release catalog identifies the three removed packages as deprecated with EOL
  2023-03-31 and maps them to the split Key Vault clients plus `Azure.Identity`.
- The configured package feed and NuGet.org were checked independently. The supported stable
  versions used are Secrets 4.11.1, Keys 4.10.1, Certificates 4.9.1, and Identity 1.21.0.
  This corrects the preliminary package/version pairing: stable Keys 4.11.1 and
  Certificates 4.10.1 do not exist.
- Official migration guides consulted:
  - https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/keyvault/Azure.Security.KeyVault.Secrets/MigrationGuide.md
  - https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/keyvault/Azure.Security.KeyVault.Keys/MigrationGuide.md
  - https://learn.microsoft.com/dotnet/api/overview/azure/app-auth-migration

## Implementation

- Retargeted `Contoso.Secrets.csproj` from `net8.0` to `net10.0`.
- Replaced the deprecated package set with only the four directly required Track 2 packages
  and removed the obsolete NU1902/NU1903 suppressions.
- Added `VaultClients`, which creates and retains one vault-bound `SecretClient`,
  `KeyClient`, and `CertificateClient`, shares one `TokenCredential`, and caches
  destination-vault `SecretClient` instances by URI.
- Preserved environment-based authentication through `DefaultAzureCredential`, explicit
  service-principal selection through `ClientSecretCredential`, and user-assigned identity
  client-ID pinning through `ManagedIdentityId.FromUserAssignedClientId`.
- Replaced direct OAuth HTTP/token parsing with `TokenCredential.GetTokenAsync` and a
  `/.default` scope. `TokenPayloadReader.cs` was removed because it became unreferenced.
  Consequently, the .NET behavioral change to `HttpContent.ReadAsStringAsync` is not
  applicable to remaining application code.
- Reimplemented `CorrelationIdHandler` as an `HttpPipelineSynchronousPolicy`. It is
  registered for the same user-assigned-identity factory path at
  `HttpPipelinePosition.PerCall`, which runs before retry processing and keeps one header
  value on all attempts belonging to the logical operation.
- Rewrote secret, key, cryptography, and certificate operations to Track 2 response,
  paging, model, and long-running-operation shapes. Delete and certificate-create methods
  retain the previous start/accept behavior rather than waiting for soft-delete or issuance
  completion.

## Public wrapper members

| Type | Before | After |
|---|---|---|
| `VaultClientFactory` | `CreateWithManagedIdentity()`, `CreateWithUserAssignedIdentity(clientId)`, `CreateWithServicePrincipal(tenantId, clientId, clientSecret)`, `GetAccessTokenAsync(resource)` | Same four named capabilities; the three client factories now also take `vaultUrl` and return `VaultClients`; `GetAccessTokenAsync(resource)` retains its signature. |
| `SecretManager` | constructor `(client, vaultBaseUrl)`; `GetConnectionStringAsync`, `GetPinnedVersionAsync`, `TryGetSecretAsync`, `SetSecretAsync`, `SetSecretWithMetadataAsync`, `DeleteSecretAsync`, `ListSecretNamesAsync`, `CopySecretToAsync` | Constructor is now `(VaultClients)`; all eight operations remain with the same names and signatures. |
| `KeyManager` | constructor `(client, vaultBaseUrl)`; `CreateRsaKeyAsync`, `GetKeyAsync`, `KeyExistsAsync`, `WrapAsync`, `UnwrapAsync`, `SignDigestAsync`, `VerifyDigestAsync`, `DeleteKeyAsync` | Constructor is now `(KeyClient)`; all eight operations remain. `GetKeyAsync` returns `KeyVaultKey` instead of the retired model type. |
| `CertificateManager` | constructor `(client, vaultBaseUrl)`; `GetPublicCertificateAsync`, `GetExpiryAsync`, `GetCertificateWithPrivateKeyAsync`, `ListCertificateNamesAsync`, `StartCreateSelfSignedAsync` | Constructor is now `(CertificateClient, SecretClient)`; all five operations remain with the same names and result types. |
| `CorrelationIdHandler` / `CorrelationScope` | Public handler type and `HeaderName`; public ambient `Current` property | Public type, constant, and ambient property remain; the implementation is now an Azure.Core policy and exposes the required `OnSendingRequest` override. |
| `VaultClients` | Not present | New public holder exposing the cached `Secrets`, `Keys`, and `Certificates` clients. |

No public wrapper operation was removed. No proven unreachable conditional branch was removed.
The internal `TokenPayloadReader` helper and the obsolete manual continuation-loop plumbing were
removed because their callers disappeared in the supported credential and pageable APIs.

## Four-shape behavior audit

| Service | Shape | Outcome | Evidence / action |
|---|---|---|---|
| Secrets | External contract | Preserved | Secret values remain strings without application serialization; PFX material remains base64-decoded by the certificate path. Console messages used by the sample remain unchanged. |
| Secrets | Operation contract | Unchecked | Source evidence preserves latest/versioned reads, tags, `text/plain`, enabled state, UTC expiry/not-before values, a page-size hint of 25, and both 404 paths (`SecretManager.cs`). Build proves API compatibility, but set/list/delete behavior was not exercised against a vault. |
| Secrets | Boundary and topology | Preserved | Source client is vault-bound and retained. `CopySecretToAsync` obtains a separately bound destination client from the URI cache before writing, so destination affinity is explicit. All eight public operations remain. |
| Secrets | Operational | Unchecked | Clients and credentials are cached/thread-safe and the user-assigned identity remains pinned. Actual identity selection, RBAC, endpoint reachability, retry behavior, and destination-vault authorization require a non-production vault. |
| Keys | External contract | Preserved | Input/output byte arrays remain unchanged; wrapping uses `KeyWrapAlgorithm.RsaOaep` and signing uses `SignatureAlgorithm.RS256`, matching the previous algorithms. |
| Keys | Operation contract | Unchecked | Source evidence preserves RSA-2048, enabled/not-before settings, all six key operations, exact-version cryptography binding, wrap/unwrap, sign/verify, and the filtered 404 existence check (`KeyManager.cs`). A live cryptographic round trip was not run. |
| Keys | Boundary and topology | Preserved | The vault-bound `KeyClient` remains long-lived; cryptographic calls moved to cached `CryptographyClient` instances keyed by the exact key ID/version. All eight public operations remain; only the SDK model return type changed. |
| Keys | Operational | Unchecked | Shared credential and transport-pool behavior follow the Track 2 contract. Actual permissions, HSM/software behavior, network policy, and soft-delete timing were not exercised. |
| Certificates | External contract | Unchecked | Source still returns DER certificate bytes and decodes the same-name secret as base64 PFX. Existing live certificate/secret payload compatibility could not be exercised without Azure access. |
| Certificates | Operation contract | Unchecked | Source evidence preserves metadata-only paging, the filtered expiry 404 path, self-signed issuer, subject, exportability, RSA-2048, no key reuse, PKCS#12 content type, 12-month validity, and returns the pending operation ID without waiting. Live issuance was not run. |
| Certificates | Boundary and topology | Preserved | The manager receives both clients bound to the same source vault, keeping the certificate-to-secret private-key lookup affinity. All five public operations remain. |
| Certificates | Operational | Unchecked | Long-lived clients share the credential and transport pool. Certificate permissions, issuer behavior, policy acceptance, network conditions, and completion semantics require a non-production vault. |

## Correlation policy evidence

- `CorrelationIdHandler.OnSendingRequest` adds `x-contoso-correlation-id` only when absent.
- `VaultClients.AddCorrelationPolicy` registers the policy with
  `HttpPipelinePosition.PerCall`; Azure.Core invokes per-call policies outside the retry
  segment, so the same stamped request header is retained across retry attempts.
- Registration is enabled only for `CreateWithUserAssignedIdentity`, matching the original
  factory path that installed the custom handler rather than broadening observable behavior
  to the other authentication modes.

## Validation evidence

- Branch: `deepchoudhery-key-vault-net10-trial`; no branch, commit, stash, or publication
  operation was performed.
- Baseline: scoped `net8.0` build succeeded with 0 warnings and 0 errors.
- Stable SDK validation:
  `dotnet exec "C:\Program Files\dotnet\sdk\10.0.303\MSBuild.dll" Contoso.Secrets.csproj /restore /t:Rebuild /p:Configuration=Release`
  succeeded and produced `bin\Release\net10.0\Contoso.Secrets.dll`.
- Standard CLI validation: Release no-incremental build succeeded with 0 warnings and 0
  errors. The machine-wide resolver selected an installed .NET 10 preview SDK because no
  `global.json` exists; the separate stable-10.0.303 validation above removes ambiguity.
- Offline smoke test: `dotnet run --project Contoso.Secrets.csproj --configuration Release
  --no-build` exited 0 and printed the expected no-vault notice without contacting Azure.
- Tests: repository search found no solution file and no project containing a test marker,
  test SDK, MSTest, xUnit, or NUnit reference; there are no affected existing tests to run.
- Legacy source/docs search over `keyvault-sample` returned zero matches for the removed
  package namespaces, client/model types, AppAuthentication name, and retired exception type.
- Exactly four filtered `RequestFailedException` 404 handlers were found: two in
  `SecretManager.cs`, one in `KeyManager.cs`, and one in `CertificateManager.cs`.
- Final transitive graph contains only the four requested top-level packages and modern
  dependencies (`Azure.Core`, MSAL, and Microsoft.Extensions/System packages); no Track 1
  Key Vault, AppAuthentication, ADAL, or Microsoft.Rest packages remain.
- `dotnet list package --vulnerable --include-transitive` reported no vulnerable packages
  from the configured sources.

## Files changed

- `keyvault-sample\Contoso.Secrets.csproj`
- `keyvault-sample\VaultClientFactory.cs`
- `keyvault-sample\CorrelationIdHandler.cs`
- `keyvault-sample\SecretManager.cs`
- `keyvault-sample\KeyManager.cs`
- `keyvault-sample\CertificateManager.cs`
- `keyvault-sample\Program.cs`
- `keyvault-sample\README.md`
- `keyvault-sample\TokenPayloadReader.cs` (deleted)
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-keyvault-sample\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-keyvault-sample\progress-details.md`

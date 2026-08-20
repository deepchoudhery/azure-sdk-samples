# Key Vault sample — `migrating-azure-keyvault`

A console app that reads configuration secrets, performs envelope encryption with a
vault-held RSA key, and loads TLS certificates. It is deliberately stuck on the retired
`Microsoft.Azure.KeyVault` SDK.

- **TFM:** `net8.0`
- **Deprecated packages:** `Microsoft.Azure.KeyVault`, `Microsoft.Azure.KeyVault.WebKey`, `Microsoft.Azure.Services.AppAuthentication`
- **Baseline:** `dotnet build` succeeds with 0 warnings, 0 errors.

## What each file exercises

| File | Legacy surface under test |
|------|---------------------------|
| `VaultClientFactory.cs` | `AzureServiceTokenProvider`, `KeyVaultClient.AuthenticationCallback`, hand-rolled OAuth token acquisition, `DelegatingHandler[]` constructor overload |
| `CorrelationIdHandler.cs` | A custom `DelegatingHandler` passed straight into `KeyVaultClient` |
| `SecretManager.cs` | `SecretBundle`, `GetSecretAsync`, versioned reads, `SetSecretAsync` with tags/content type/`SecretAttributes`, `DeleteSecretAsync`, `IPage<SecretItem>` + `GetSecretsNextAsync` paging, `KeyVaultErrorException` 404 handling, cross-vault copy |
| `KeyManager.cs` | `KeyBundle`, `CreateKeyAsync`, `JsonWebKeyType` / `JsonWebKeyOperation` / `JsonWebKeyEncryptionAlgorithm` / `JsonWebKeySignatureAlgorithm`, `WrapKeyAsync`, `UnwrapKeyAsync`, `SignAsync`, `VerifyAsync` |
| `CertificateManager.cs` | `CertificateBundle`, `GetCertificateAsync`, `IPage<CertificateItem>` paging, PFX-via-secret round trip, `CertificatePolicy` + `CreateCertificateAsync` |

## Expected migration outcome

### Packages

- [ ] `Microsoft.Azure.KeyVault`, `Microsoft.Azure.KeyVault.WebKey`, and `Microsoft.Azure.Services.AppAuthentication` are all removed from `Contoso.Secrets.csproj`.
- [ ] `Azure.Security.KeyVault.Secrets`, `Azure.Security.KeyVault.Keys`, `Azure.Security.KeyVault.Certificates`, and `Azure.Identity` are added. All three resource packages are genuinely needed here — dropping one is a miss, adding a fourth unused one is also a miss.
- [ ] The `NU1902;NU1903` suppression is no longer needed and should be dropped.

### Authentication

- [ ] `AzureServiceTokenProvider` + `KeyVaultClient.AuthenticationCallback` becomes `DefaultAzureCredential`.
- [ ] `CreateWithUserAssignedIdentity` becomes `ManagedIdentityCredential` (or `DefaultAzureCredential` with `ManagedIdentityClientId` set) — **not** a bare `DefaultAzureCredential`, which would silently lose the identity pinning.
- [ ] `CreateWithServicePrincipal` becomes `ClientSecretCredential`, and the hand-rolled `HttpClient` token call plus `TokenPayloadReader` are deleted outright.
- [ ] `GetAccessTokenAsync` becomes `credential.GetTokenAsync(new TokenRequestContext(scopes))`.

### Client shape

- [ ] One `SecretClient` / `KeyClient` / `CertificateClient` per vault URI, constructed once.
- [ ] `_vaultBaseUrl` is no longer threaded through every call.
- [ ] `CopySecretToAsync` creates a **second** `SecretClient` for the destination vault. Reusing the source client here is a correctness bug, because the vault URI is now fixed at construction.

### Operations

- [ ] `SecretBundle` → `KeyVaultSecret`; `KeyBundle` → `KeyVaultKey`; `CertificateBundle` → `KeyVaultCertificateWithPolicy`.
- [ ] `IPage<T>` + `Get*NextAsync` loops collapse to `await foreach` over `AsyncPageable<SecretProperties>` / `AsyncPageable<CertificateProperties>`. A migration that keeps a manual `NextPageLink` loop has not really migrated.
- [ ] `SetSecretAsync(url, name, value, tags, contentType, attributes)` becomes a `KeyVaultSecret` with `Properties.Tags`, `Properties.ContentType`, `Properties.ExpiresOn`, `Properties.NotBefore`, then `SetSecretAsync(secret)`.
- [ ] `DeleteSecretAsync` returns a `DeleteSecretOperation` now — the caller either awaits completion or explicitly does not.
- [ ] Wrap/unwrap and sign/verify move to `CryptographyClient` (`keyClient.GetCryptographyClient(name)` or `new CryptographyClient(keyId, credential)`). Leaving these on `KeyClient` will not compile, so watch for them being dropped instead of ported.
- [ ] `JsonWebKeyType.Rsa` → `KeyType.Rsa`; `JsonWebKeyOperation.*` → `KeyOperation.*`; `JsonWebKeyEncryptionAlgorithm.RSAOAEP` → `KeyWrapAlgorithm.RsaOaep`; `JsonWebKeySignatureAlgorithm.RS256` → `SignatureAlgorithm.RS256`.
- [ ] `CertificatePolicy` is rebuilt using the new `CertificatePolicy` type (`CertificatePolicy.Default` or the issuer/subject constructor), and `CreateCertificateAsync` returns a `CertificateOperation`.

### Error handling

- [ ] Every `catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)` becomes `catch (RequestFailedException ex) when (ex.Status == 404)`.
- [ ] There are **four** of these (`TryGetSecretAsync`, `DeleteSecretAsync`, `KeyExistsAsync`, `GetExpiryAsync`). Missing any one is a partial migration.

### Custom pipeline

- [ ] `CorrelationIdHandler` is either reimplemented as an `HttpPipelinePolicy` registered through `SecretClientOptions.AddPolicy(...)`, or explicitly called out as dropped. Silently deleting it without a note is a miss — it changes observable request behavior.

### Build

- [ ] `dotnet build` succeeds.
- [ ] No `Microsoft.Azure.KeyVault` or `Microsoft.Azure.Services.AppAuthentication` references survive anywhere: `git grep -n "Microsoft.Azure.KeyVault\|AppAuthentication\|KeyVaultClient\|SecretBundle\|KeyBundle\|CertificateBundle"` returns nothing.

## Running

```powershell
dotnet build
$env:CONTOSO_VAULT_URL = "https://contoso-dev.vault.azure.net/"   # optional
dotnet run
```

Without `CONTOSO_VAULT_URL` the app prints a notice and exits without touching the network.

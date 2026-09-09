# Key Vault sample — .NET 10 and Azure SDK Track 2

A .NET 10 console app that reads and writes secrets, performs envelope encryption
with a vault-held RSA key, and reads or creates certificates.

## Packages

- `Azure.Security.KeyVault.Secrets`
- `Azure.Security.KeyVault.Keys`
- `Azure.Security.KeyVault.Certificates`
- `Azure.Identity`

## Authentication modes

`VaultClientFactory` retains four authentication capabilities:

- `CreateWithManagedIdentity(vaultUrl)` uses `DefaultAzureCredential` so the same
  code supports local developer credentials and Azure managed identity.
- `CreateWithUserAssignedIdentity(vaultUrl, clientId)` uses
  `ManagedIdentityCredential` with the supplied client ID, preventing fallback to a
  different managed identity. This mode also registers the correlation policy.
- `CreateWithServicePrincipal(vaultUrl, tenantId, clientId, clientSecret)` uses
  `ClientSecretCredential` and preserves the explicitly selected principal.
- `GetAccessTokenAsync(resource)` requests the resource's `/.default` scope and
  returns the bearer token text for callers that explicitly need it.

The factory creates one long-lived `SecretClient`, `KeyClient`, and
`CertificateClient` for the source vault. Destination `SecretClient` instances used
by cross-vault copies are cached by vault URI and share the same credential.

## Preserved operations

- Latest and version-pinned secret reads, tagged secret writes, idempotent deletion,
  metadata-only paged listing, and cross-vault copies.
- RSA-2048 key creation with encrypt, decrypt, sign, verify, wrap, and unwrap
  operations.
- RSA-OAEP key wrapping and RS256 digest signing through cached
  `CryptographyClient` instances bound to the exact key version.
- Public-certificate reads, PFX retrieval through the certificate's same-name
  secret, metadata-only certificate listing, and asynchronous self-signed
  certificate creation.
- Missing secret, idempotent secret deletion, missing key, and missing certificate
  paths filter `RequestFailedException` on HTTP status 404.
- `CorrelationIdHandler` is an Azure.Core synchronous pipeline policy registered at
  `HttpPipelinePosition.PerCall`. The header is stamped once before retry processing,
  so every retry for one logical operation carries the same correlation value.

## Build

```powershell
dotnet restore .\Contoso.Secrets.csproj
dotnet build .\Contoso.Secrets.csproj
```

## Running

```powershell
$env:CONTOSO_VAULT_URL = "https://contoso-dev.vault.azure.net/"
dotnet run --project .\Contoso.Secrets.csproj
```

Without `CONTOSO_VAULT_URL`, the app prints a notice and exits without contacting
Azure. Live-service behavior requires a non-production test vault and appropriate
data-plane role assignments; it is not exercised by the build.

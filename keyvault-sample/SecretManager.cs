using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest.Azure;

namespace Contoso.Secrets
{
    /// <summary>
    /// Reads and writes secrets. Every call has to repeat the vault base URL because the
    /// legacy client is vault-agnostic.
    /// </summary>
    public class SecretManager
    {
        private readonly KeyVaultClient _client;
        private readonly string _vaultBaseUrl;

        public SecretManager(KeyVaultClient client, string vaultBaseUrl)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _vaultBaseUrl = vaultBaseUrl ?? throw new ArgumentNullException(nameof(vaultBaseUrl));
        }

        public async Task<string> GetConnectionStringAsync(string secretName)
        {
            SecretBundle secret = await _client.GetSecretAsync(_vaultBaseUrl, secretName)
                .ConfigureAwait(false);

            return secret.Value;
        }

        /// <summary>
        /// Pins a read to a specific secret version.
        /// </summary>
        public async Task<string> GetPinnedVersionAsync(string secretName, string version)
        {
            SecretBundle secret = await _client
                .GetSecretAsync(_vaultBaseUrl, secretName, version)
                .ConfigureAwait(false);

            return secret.Value;
        }

        /// <summary>
        /// Returns <c>null</c> instead of throwing when the secret is absent. The legacy SDK
        /// signals this through <see cref="KeyVaultErrorException"/> and an HTTP status code.
        /// </summary>
        public async Task<string> TryGetSecretAsync(string secretName)
        {
            try
            {
                SecretBundle secret = await _client.GetSecretAsync(_vaultBaseUrl, secretName)
                    .ConfigureAwait(false);

                return secret.Value;
            }
            catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<string> SetSecretAsync(string secretName, string value)
        {
            SecretBundle stored = await _client
                .SetSecretAsync(_vaultBaseUrl, secretName, value)
                .ConfigureAwait(false);

            return stored.SecretIdentifier.Identifier;
        }

        /// <summary>
        /// Writes a secret with tags, a content type, and an expiry.
        /// </summary>
        public async Task<string> SetSecretWithMetadataAsync(
            string secretName,
            string value,
            string owner,
            DateTime expiresOnUtc)
        {
            var tags = new Dictionary<string, string>
            {
                ["owner"] = owner,
                ["managed-by"] = "contoso-secrets",
            };

            var attributes = new SecretAttributes
            {
                Enabled = true,
                Expires = expiresOnUtc,
                NotBefore = DateTime.UtcNow,
            };

            SecretBundle stored = await _client
                .SetSecretAsync(_vaultBaseUrl, secretName, value, tags, "text/plain", attributes)
                .ConfigureAwait(false);

            return stored.SecretIdentifier.Identifier;
        }

        public async Task DeleteSecretAsync(string secretName)
        {
            try
            {
                await _client.DeleteSecretAsync(_vaultBaseUrl, secretName).ConfigureAwait(false);
            }
            catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                // Already gone; deleting is idempotent from the caller's point of view.
            }
        }

        /// <summary>
        /// Enumerates every secret name in the vault. The legacy SDK pages with an opaque
        /// "next page link" that must be fed back into a distinct <c>*Next</c> method.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListSecretNamesAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var names = new List<string>();

            IPage<SecretItem> page = await _client
                .GetSecretsAsync(_vaultBaseUrl, 25, cancellationToken)
                .ConfigureAwait(false);

            while (page != null)
            {
                foreach (SecretItem item in page)
                {
                    names.Add(item.Identifier.Name);
                }

                if (string.IsNullOrEmpty(page.NextPageLink))
                {
                    break;
                }

                page = await _client
                    .GetSecretsNextAsync(page.NextPageLink, cancellationToken)
                    .ConfigureAwait(false);
            }

            return names;
        }

        /// <summary>
        /// Copies a secret from one vault to another. Exercises the "one client, many vaults"
        /// shape that the legacy SDK allowed and the modern SDK does not.
        /// </summary>
        public async Task CopySecretToAsync(string secretName, string destinationVaultUrl)
        {
            SecretBundle source = await _client.GetSecretAsync(_vaultBaseUrl, secretName)
                .ConfigureAwait(false);

            await _client.SetSecretAsync(destinationVaultUrl, secretName, source.Value)
                .ConfigureAwait(false);
        }
    }
}

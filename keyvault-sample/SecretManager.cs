using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Contoso.Secrets
{
    /// <summary>
    /// Reads and writes secrets through a client bound to one vault.
    /// </summary>
    public class SecretManager
    {
        private readonly VaultClients _clients;
        private readonly SecretClient _client;

        public SecretManager(VaultClients clients)
        {
            _clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _client = clients.Secrets;
        }

        public async Task<string> GetConnectionStringAsync(string secretName)
        {
            KeyVaultSecret secret = await _client.GetSecretAsync(secretName)
                .ConfigureAwait(false);

            return secret.Value;
        }

        /// <summary>
        /// Pins a read to a specific secret version.
        /// </summary>
        public async Task<string> GetPinnedVersionAsync(string secretName, string version)
        {
            KeyVaultSecret secret = await _client
                .GetSecretAsync(secretName, version)
                .ConfigureAwait(false);

            return secret.Value;
        }

        /// <summary>
        /// Returns <c>null</c> instead of throwing when the secret is absent.
        /// </summary>
        public async Task<string> TryGetSecretAsync(string secretName)
        {
            try
            {
                KeyVaultSecret secret = await _client.GetSecretAsync(secretName)
                    .ConfigureAwait(false);

                return secret.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<string> SetSecretAsync(string secretName, string value)
        {
            KeyVaultSecret stored = await _client
                .SetSecretAsync(secretName, value)
                .ConfigureAwait(false);

            return stored.Id.AbsoluteUri;
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

            var secret = new KeyVaultSecret(secretName, value)
            {
                Properties =
                {
                    Enabled = true,
                    ExpiresOn = new DateTimeOffset(
                        DateTime.SpecifyKind(expiresOnUtc, DateTimeKind.Utc)),
                    NotBefore = DateTimeOffset.UtcNow,
                    ContentType = "text/plain",
                },
            };

            foreach (KeyValuePair<string, string> tag in tags)
            {
                secret.Properties.Tags.Add(tag);
            }

            KeyVaultSecret stored = await _client
                .SetSecretAsync(secret)
                .ConfigureAwait(false);

            return stored.Id.AbsoluteUri;
        }

        public async Task DeleteSecretAsync(string secretName)
        {
            try
            {
                await _client.StartDeleteSecretAsync(secretName).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
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

            await foreach (Page<SecretProperties> page in _client
                .GetPropertiesOfSecretsAsync(cancellationToken)
                .AsPages(pageSizeHint: 25))
            {
                foreach (SecretProperties item in page.Values)
                {
                    names.Add(item.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Copies a secret from one vault to another. Exercises the "one client, many vaults"
        /// shape that the legacy SDK allowed and the modern SDK does not.
        /// </summary>
        public async Task CopySecretToAsync(string secretName, string destinationVaultUrl)
        {
            KeyVaultSecret source = await _client.GetSecretAsync(secretName)
                .ConfigureAwait(false);

            SecretClient destination = _clients.GetSecretClient(destinationVaultUrl);
            await destination.SetSecretAsync(secretName, source.Value)
                .ConfigureAwait(false);
        }
    }
}

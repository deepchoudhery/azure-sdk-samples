using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;

namespace Contoso.Secrets
{
    public static class Program
    {
        private const string VaultUrlVariable = "CONTOSO_VAULT_URL";

        public static async Task<int> Main(string[] args)
        {
            var vaultUrl = Environment.GetEnvironmentVariable(VaultUrlVariable);

            if (string.IsNullOrWhiteSpace(vaultUrl))
            {
                Console.WriteLine(
                    $"Set {VaultUrlVariable} (for example https://contoso-dev.vault.azure.net/) to run against a real vault.");
                Console.WriteLine("Nothing to do — exiting without contacting Azure.");
                return 0;
            }

            KeyVaultClient client = VaultClientFactory.CreateWithManagedIdentity();

            var secrets = new SecretManager(client, vaultUrl);
            var keys = new KeyManager(client, vaultUrl);
            var certificates = new CertificateManager(client, vaultUrl);

            try
            {
                await DumpSecretsAsync(secrets).ConfigureAwait(false);
                await DumpKeysAsync(keys).ConfigureAwait(false);
                await DumpCertificatesAsync(certificates).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Vault operation failed: {ex.Message}");
                return 1;
            }

            return 0;
        }

        private static async Task DumpSecretsAsync(SecretManager secrets)
        {
            IReadOnlyList<string> names = await secrets.ListSecretNamesAsync().ConfigureAwait(false);
            Console.WriteLine($"Vault holds {names.Count} secret(s).");

            var sqlConnection = await secrets.TryGetSecretAsync("sql-connection-string")
                .ConfigureAwait(false);

            Console.WriteLine(sqlConnection is null
                ? "sql-connection-string is not provisioned."
                : "sql-connection-string resolved.");

            await secrets
                .SetSecretWithMetadataAsync(
                    "last-startup",
                    DateTime.UtcNow.ToString("O"),
                    "platform-team",
                    DateTime.UtcNow.AddDays(30))
                .ConfigureAwait(false);
        }

        private static async Task DumpKeysAsync(KeyManager keys)
        {
            if (!await keys.KeyExistsAsync("data-protection").ConfigureAwait(false))
            {
                Console.WriteLine("data-protection key missing; creating it.");
                await keys.CreateRsaKeyAsync("data-protection").ConfigureAwait(false);
            }

            var dek = Guid.NewGuid().ToByteArray();
            var wrapped = await keys.WrapAsync("data-protection", dek).ConfigureAwait(false);
            var unwrapped = await keys.UnwrapAsync("data-protection", wrapped).ConfigureAwait(false);

            Console.WriteLine($"Envelope round trip preserved key: {AreEqual(dek, unwrapped)}");
        }

        private static async Task DumpCertificatesAsync(CertificateManager certificates)
        {
            IReadOnlyList<string> names = await certificates.ListCertificateNamesAsync()
                .ConfigureAwait(false);

            Console.WriteLine($"Vault holds {names.Count} certificate(s).");

            foreach (var name in names)
            {
                DateTime? expires = await certificates.GetExpiryAsync(name).ConfigureAwait(false);
                Console.WriteLine($"  {name} expires {expires?.ToString("u") ?? "never"}");
            }
        }

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (left is null || right is null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

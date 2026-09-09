using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;

namespace Contoso.Secrets
{
    /// <summary>
    /// Reads TLS certificates out of the vault so the host can bind them at startup.
    /// </summary>
    public class CertificateManager
    {
        private readonly CertificateClient _client;
        private readonly SecretClient _secretClient;

        public CertificateManager(CertificateClient client, SecretClient secretClient)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        }

        public async Task<byte[]> GetPublicCertificateAsync(string certificateName)
        {
            KeyVaultCertificateWithPolicy certificate = await _client
                .GetCertificateAsync(certificateName)
                .ConfigureAwait(false);

            return certificate.Cer;
        }

        public async Task<DateTime?> GetExpiryAsync(string certificateName)
        {
            try
            {
                KeyVaultCertificateWithPolicy certificate = await _client
                    .GetCertificateAsync(certificateName)
                    .ConfigureAwait(false);

                return certificate.Properties.ExpiresOn?.UtcDateTime;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        /// <summary>
        /// The private key of a vault certificate is exposed as a secret with the same name.
        /// This round trip is a very common legacy pattern for loading a PFX.
        /// </summary>
        public async Task<byte[]> GetCertificateWithPrivateKeyAsync(string certificateName)
        {
            KeyVaultSecret secret = await _secretClient
                .GetSecretAsync(certificateName)
                .ConfigureAwait(false);

            return Convert.FromBase64String(secret.Value);
        }

        public async Task<IReadOnlyList<string>> ListCertificateNamesAsync()
        {
            var names = new List<string>();

            await foreach (CertificateProperties certificate in
                _client.GetPropertiesOfCertificatesAsync())
            {
                names.Add(certificate.Name);
            }

            return names;
        }

        /// <summary>
        /// Kicks off a self-signed certificate creation and returns the pending operation id.
        /// </summary>
        public async Task<string> StartCreateSelfSignedAsync(string certificateName, string subject)
        {
            var policy = new CertificatePolicy(
                WellKnownIssuerNames.Self,
                subject)
            {
                Exportable = true,
                KeyType = CertificateKeyType.Rsa,
                KeySize = 2048,
                ReuseKey = false,
                ContentType = CertificateContentType.Pkcs12,
                ValidityInMonths = 12,
            };

            CertificateOperation operation = await _client
                .StartCreateCertificateAsync(certificateName, policy)
                .ConfigureAwait(false);

            return operation.Id;
        }
    }
}

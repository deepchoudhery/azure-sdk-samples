using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest.Azure;

namespace Contoso.Secrets
{
    /// <summary>
    /// Reads TLS certificates out of the vault so the host can bind them at startup.
    /// </summary>
    public class CertificateManager
    {
        private readonly KeyVaultClient _client;
        private readonly string _vaultBaseUrl;

        public CertificateManager(KeyVaultClient client, string vaultBaseUrl)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _vaultBaseUrl = vaultBaseUrl ?? throw new ArgumentNullException(nameof(vaultBaseUrl));
        }

        public async Task<byte[]> GetPublicCertificateAsync(string certificateName)
        {
            CertificateBundle bundle = await _client
                .GetCertificateAsync(_vaultBaseUrl, certificateName)
                .ConfigureAwait(false);

            return bundle.Cer;
        }

        public async Task<DateTime?> GetExpiryAsync(string certificateName)
        {
            try
            {
                CertificateBundle bundle = await _client
                    .GetCertificateAsync(_vaultBaseUrl, certificateName)
                    .ConfigureAwait(false);

                return bundle.Attributes?.Expires;
            }
            catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
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
            SecretBundle secret = await _client
                .GetSecretAsync(_vaultBaseUrl, certificateName)
                .ConfigureAwait(false);

            return Convert.FromBase64String(secret.Value);
        }

        public async Task<IReadOnlyList<string>> ListCertificateNamesAsync()
        {
            var names = new List<string>();

            IPage<CertificateItem> page = await _client
                .GetCertificatesAsync(_vaultBaseUrl)
                .ConfigureAwait(false);

            while (page != null)
            {
                foreach (CertificateItem item in page)
                {
                    names.Add(item.Identifier.Name);
                }

                if (string.IsNullOrEmpty(page.NextPageLink))
                {
                    break;
                }

                page = await _client.GetCertificatesNextAsync(page.NextPageLink)
                    .ConfigureAwait(false);
            }

            return names;
        }

        /// <summary>
        /// Kicks off a self-signed certificate creation and returns the pending operation id.
        /// </summary>
        public async Task<string> StartCreateSelfSignedAsync(string certificateName, string subject)
        {
            var policy = new CertificatePolicy
            {
                IssuerParameters = new IssuerParameters { Name = "Self" },
                KeyProperties = new KeyProperties
                {
                    Exportable = true,
                    KeyType = "RSA",
                    KeySize = 2048,
                    ReuseKey = false,
                },
                SecretProperties = new SecretProperties { ContentType = "application/x-pkcs12" },
                X509CertificateProperties = new X509CertificateProperties
                {
                    Subject = subject,
                    ValidityInMonths = 12,
                },
            };

            CertificateOperation operation = await _client
                .CreateCertificateAsync(_vaultBaseUrl, certificateName, policy)
                .ConfigureAwait(false);

            return operation.Id;
        }
    }
}

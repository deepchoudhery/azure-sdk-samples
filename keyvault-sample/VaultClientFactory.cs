using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Secrets;

namespace Contoso.Secrets
{
    /// <summary>
    /// Builds and caches the vault-bound Track 2 clients used by the sample.
    /// </summary>
    public static class VaultClientFactory
    {
        private static readonly DefaultAzureCredential DefaultCredential =
            new DefaultAzureCredential();

        /// <summary>
        /// Managed identity with local-developer fallback.
        /// </summary>
        public static VaultClients CreateWithManagedIdentity(string vaultUrl)
        {
            return new VaultClients(vaultUrl, DefaultCredential, addCorrelationPolicy: false);
        }

        /// <summary>
        /// Uses a specific user-assigned managed identity and retains correlation headers.
        /// </summary>
        public static VaultClients CreateWithUserAssignedIdentity(string vaultUrl, string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A client id is required.", nameof(clientId));
            }

            return new VaultClients(
                vaultUrl,
                new ManagedIdentityCredential(
                    ManagedIdentityId.FromUserAssignedClientId(clientId)),
                addCorrelationPolicy: true);
        }

        /// <summary>
        /// Explicit service principal authentication.
        /// </summary>
        public static VaultClients CreateWithServicePrincipal(
            string vaultUrl,
            string tenantId,
            string clientId,
            string clientSecret)
        {
            return new VaultClients(
                vaultUrl,
                new ClientSecretCredential(tenantId, clientId, clientSecret),
                addCorrelationPolicy: false);
        }

        /// <summary>
        /// Directly requests a bearer token for an arbitrary resource.
        /// </summary>
        public static async Task<string> GetAccessTokenAsync(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException("A resource is required.", nameof(resource));
            }

            var context = new TokenRequestContext(
                new[] { resource.TrimEnd('/') + "/.default" });
            AccessToken token = await DefaultCredential
                .GetTokenAsync(context, CancellationToken.None)
                .ConfigureAwait(false);

            return token.Token;
        }
    }

    /// <summary>
    /// Long-lived Key Vault clients that share one credential and Azure.Core transport pool.
    /// </summary>
    public sealed class VaultClients
    {
        private readonly TokenCredential _credential;
        private readonly bool _addCorrelationPolicy;
        private readonly ConcurrentDictionary<string, SecretClient> _secretClients;

        internal VaultClients(
            string vaultUrl,
            TokenCredential credential,
            bool addCorrelationPolicy)
        {
            if (string.IsNullOrWhiteSpace(vaultUrl))
            {
                throw new ArgumentException("A vault URL is required.", nameof(vaultUrl));
            }

            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _addCorrelationPolicy = addCorrelationPolicy;
            _secretClients = new ConcurrentDictionary<string, SecretClient>(
                StringComparer.OrdinalIgnoreCase);

            Uri vaultUri = new Uri(vaultUrl, UriKind.Absolute);
            Secrets = CreateSecretClient(vaultUri);
            _secretClients.TryAdd(vaultUri.AbsoluteUri, Secrets);
            Keys = new KeyClient(vaultUri, _credential, CreateKeyOptions());
            Certificates = new CertificateClient(
                vaultUri,
                _credential,
                CreateCertificateOptions());
        }

        public SecretClient Secrets { get; }

        public KeyClient Keys { get; }

        public CertificateClient Certificates { get; }

        internal SecretClient GetSecretClient(string vaultUrl)
        {
            if (string.IsNullOrWhiteSpace(vaultUrl))
            {
                throw new ArgumentException("A destination vault URL is required.", nameof(vaultUrl));
            }

            Uri vaultUri = new Uri(vaultUrl, UriKind.Absolute);
            return _secretClients.GetOrAdd(
                vaultUri.AbsoluteUri,
                _ => CreateSecretClient(vaultUri));
        }

        private SecretClient CreateSecretClient(Uri vaultUri)
        {
            return new SecretClient(vaultUri, _credential, CreateSecretOptions());
        }

        private SecretClientOptions CreateSecretOptions()
        {
            var options = new SecretClientOptions();
            AddCorrelationPolicy(options);
            return options;
        }

        private KeyClientOptions CreateKeyOptions()
        {
            var options = new KeyClientOptions();
            AddCorrelationPolicy(options);
            return options;
        }

        private CertificateClientOptions CreateCertificateOptions()
        {
            var options = new CertificateClientOptions();
            AddCorrelationPolicy(options);
            return options;
        }

        private void AddCorrelationPolicy(ClientOptions options)
        {
            if (_addCorrelationPolicy)
            {
                options.AddPolicy(
                    new CorrelationIdHandler(),
                    HttpPipelinePosition.PerCall);
            }
        }
    }
}

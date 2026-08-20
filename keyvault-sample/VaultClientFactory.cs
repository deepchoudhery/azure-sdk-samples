using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;

namespace Contoso.Secrets
{
    /// <summary>
    /// Builds <see cref="KeyVaultClient"/> instances. The legacy SDK has no unified
    /// credential type, so every authentication mode needs its own token callback.
    /// </summary>
    public static class VaultClientFactory
    {
        /// <summary>
        /// Managed identity with local-developer fallback. This is the pattern the
        /// Microsoft.Azure.Services.AppAuthentication docs recommended.
        /// </summary>
        public static KeyVaultClient CreateWithManagedIdentity()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();

            return new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));
        }

        /// <summary>
        /// Same as above but pinned to a specific user-assigned managed identity via the
        /// AppAuthentication connection string format.
        /// </summary>
        public static KeyVaultClient CreateWithUserAssignedIdentity(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A client id is required.", nameof(clientId));
            }

            var tokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={clientId}");

            return new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback),
                new CorrelationIdHandler());
        }

        /// <summary>
        /// Explicit service principal. Note the hand-rolled token acquisition: the legacy
        /// SDK pushes all of this onto the caller.
        /// </summary>
        public static KeyVaultClient CreateWithServicePrincipal(
            string tenantId,
            string clientId,
            string clientSecret)
        {
            KeyVaultClient.AuthenticationCallback callback = async (authority, resource, scope) =>
            {
                using (var http = new HttpClient())
                {
                    var form = new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret,
                        ["resource"] = resource,
                    };

                    var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/token";
                    var response = await http
                        .PostAsync(tokenEndpoint, new FormUrlEncodedContent(form))
                        .ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();

                    var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return TokenPayloadReader.ReadAccessToken(payload);
                }
            };

            return new KeyVaultClient(callback);
        }

        /// <summary>
        /// Directly requests a bearer token for an arbitrary resource, bypassing the vault
        /// client entirely.
        /// </summary>
        public static Task<string> GetAccessTokenAsync(string resource)
        {
            var provider = new AzureServiceTokenProvider();
            return provider.GetAccessTokenAsync(resource);
        }
    }
}

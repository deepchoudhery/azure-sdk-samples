using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;

namespace Contoso.Secrets
{
    /// <summary>
    /// Creates keys and performs wrap/unwrap and sign/verify operations against them.
    /// </summary>
    public class KeyManager
    {
        private readonly KeyVaultClient _client;
        private readonly string _vaultBaseUrl;

        public KeyManager(KeyVaultClient client, string vaultBaseUrl)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _vaultBaseUrl = vaultBaseUrl ?? throw new ArgumentNullException(nameof(vaultBaseUrl));
        }

        public async Task<string> CreateRsaKeyAsync(string keyName)
        {
            var attributes = new KeyAttributes
            {
                Enabled = true,
                NotBefore = DateTime.UtcNow,
            };

            var operations = new List<string>
            {
                JsonWebKeyOperation.Encrypt,
                JsonWebKeyOperation.Decrypt,
                JsonWebKeyOperation.Sign,
                JsonWebKeyOperation.Verify,
                JsonWebKeyOperation.Wrap,
                JsonWebKeyOperation.Unwrap,
            };

            KeyBundle created = await _client.CreateKeyAsync(
                _vaultBaseUrl,
                keyName,
                JsonWebKeyType.Rsa,
                2048,
                operations,
                attributes).ConfigureAwait(false);

            return created.KeyIdentifier.Identifier;
        }

        public async Task<KeyBundle> GetKeyAsync(string keyName)
        {
            return await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);
        }

        public async Task<bool> KeyExistsAsync(string keyName)
        {
            try
            {
                await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);
                return true;
            }
            catch (KeyVaultErrorException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <summary>
        /// Envelope-encrypts a data encryption key using a vault-held RSA key.
        /// </summary>
        public async Task<byte[]> WrapAsync(string keyName, byte[] dataEncryptionKey)
        {
            KeyBundle key = await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);

            KeyOperationResult result = await _client.WrapKeyAsync(
                _vaultBaseUrl,
                keyName,
                key.KeyIdentifier.Version,
                JsonWebKeyEncryptionAlgorithm.RSAOAEP,
                dataEncryptionKey).ConfigureAwait(false);

            return result.Result;
        }

        public async Task<byte[]> UnwrapAsync(string keyName, byte[] wrappedKey)
        {
            KeyBundle key = await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);

            KeyOperationResult result = await _client.UnwrapKeyAsync(
                _vaultBaseUrl,
                keyName,
                key.KeyIdentifier.Version,
                JsonWebKeyEncryptionAlgorithm.RSAOAEP,
                wrappedKey).ConfigureAwait(false);

            return result.Result;
        }

        public async Task<byte[]> SignDigestAsync(string keyName, byte[] digest)
        {
            KeyBundle key = await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);

            KeyOperationResult result = await _client.SignAsync(
                _vaultBaseUrl,
                keyName,
                key.KeyIdentifier.Version,
                JsonWebKeySignatureAlgorithm.RS256,
                digest).ConfigureAwait(false);

            return result.Result;
        }

        public async Task<bool> VerifyDigestAsync(string keyName, byte[] digest, byte[] signature)
        {
            KeyBundle key = await _client.GetKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);

            KeyVerifyResult result = await _client.VerifyAsync(
                _vaultBaseUrl,
                keyName,
                key.KeyIdentifier.Version,
                JsonWebKeySignatureAlgorithm.RS256,
                digest,
                signature).ConfigureAwait(false);

            return result.Value ?? false;
        }

        public async Task DeleteKeyAsync(string keyName)
        {
            await _client.DeleteKeyAsync(_vaultBaseUrl, keyName).ConfigureAwait(false);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Contoso.Secrets
{
    /// <summary>
    /// Creates keys and performs wrap/unwrap and sign/verify operations against them.
    /// </summary>
    public class KeyManager
    {
        private readonly KeyClient _client;
        private readonly ConcurrentDictionary<string, CryptographyClient> _cryptographyClients =
            new ConcurrentDictionary<string, CryptographyClient>(StringComparer.Ordinal);

        public KeyManager(KeyClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<string> CreateRsaKeyAsync(string keyName)
        {
            var options = new CreateRsaKeyOptions(keyName)
            {
                Enabled = true,
                NotBefore = DateTimeOffset.UtcNow,
                KeySize = 2048,
            };

            options.KeyOperations.Add(KeyOperation.Encrypt);
            options.KeyOperations.Add(KeyOperation.Decrypt);
            options.KeyOperations.Add(KeyOperation.Sign);
            options.KeyOperations.Add(KeyOperation.Verify);
            options.KeyOperations.Add(KeyOperation.WrapKey);
            options.KeyOperations.Add(KeyOperation.UnwrapKey);

            KeyVaultKey created = await _client.CreateRsaKeyAsync(options).ConfigureAwait(false);
            return created.Id.AbsoluteUri;
        }

        public async Task<KeyVaultKey> GetKeyAsync(string keyName)
        {
            return await _client.GetKeyAsync(keyName).ConfigureAwait(false);
        }

        public async Task<bool> KeyExistsAsync(string keyName)
        {
            try
            {
                await _client.GetKeyAsync(keyName).ConfigureAwait(false);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return false;
            }
        }

        /// <summary>
        /// Envelope-encrypts a data encryption key using a vault-held RSA key.
        /// </summary>
        public async Task<byte[]> WrapAsync(string keyName, byte[] dataEncryptionKey)
        {
            KeyVaultKey key = await _client.GetKeyAsync(keyName).ConfigureAwait(false);
            CryptographyClient cryptographyClient = GetCryptographyClient(key);
            WrapResult result = await cryptographyClient
                .WrapKeyAsync(KeyWrapAlgorithm.RsaOaep, dataEncryptionKey)
                .ConfigureAwait(false);

            return result.EncryptedKey;
        }

        public async Task<byte[]> UnwrapAsync(string keyName, byte[] wrappedKey)
        {
            KeyVaultKey key = await _client.GetKeyAsync(keyName).ConfigureAwait(false);
            CryptographyClient cryptographyClient = GetCryptographyClient(key);
            UnwrapResult result = await cryptographyClient
                .UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep, wrappedKey)
                .ConfigureAwait(false);

            return result.Key;
        }

        public async Task<byte[]> SignDigestAsync(string keyName, byte[] digest)
        {
            KeyVaultKey key = await _client.GetKeyAsync(keyName).ConfigureAwait(false);
            CryptographyClient cryptographyClient = GetCryptographyClient(key);
            SignResult result = await cryptographyClient
                .SignAsync(SignatureAlgorithm.RS256, digest)
                .ConfigureAwait(false);

            return result.Signature;
        }

        public async Task<bool> VerifyDigestAsync(string keyName, byte[] digest, byte[] signature)
        {
            KeyVaultKey key = await _client.GetKeyAsync(keyName).ConfigureAwait(false);
            CryptographyClient cryptographyClient = GetCryptographyClient(key);
            VerifyResult result = await cryptographyClient
                .VerifyAsync(SignatureAlgorithm.RS256, digest, signature)
                .ConfigureAwait(false);

            return result.IsValid;
        }

        public async Task DeleteKeyAsync(string keyName)
        {
            await _client.StartDeleteKeyAsync(keyName).ConfigureAwait(false);
        }

        private CryptographyClient GetCryptographyClient(KeyVaultKey key)
        {
            string cacheKey = key.Id.AbsoluteUri;
            return _cryptographyClients.GetOrAdd(
                cacheKey,
                _ => _client.GetCryptographyClient(key.Name, key.Properties.Version));
        }
    }
}

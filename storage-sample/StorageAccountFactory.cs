using System;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Contoso.Documents
{
    /// <summary>
    /// Single entry point for every storage service.
    /// </summary>
    public class StorageAccountFactory
    {
        private readonly string _connectionString;
        private readonly string _accountName;
        private readonly StorageSharedKeyCredential _sharedKeyCredential;
        private readonly TableSharedKeyCredential _tableSharedKeyCredential;
        private readonly AzureSasCredential _sasCredential;

        public StorageAccountFactory(string connectionString)
        {
            _ = new BlobServiceClient(connectionString);
            _connectionString = connectionString;
        }

        private StorageAccountFactory(
            string accountName,
            StorageSharedKeyCredential sharedKeyCredential,
            TableSharedKeyCredential tableSharedKeyCredential,
            AzureSasCredential sasCredential)
        {
            _accountName = accountName;
            _sharedKeyCredential = sharedKeyCredential;
            _tableSharedKeyCredential = tableSharedKeyCredential;
            _sasCredential = sasCredential;
        }

        /// <summary>
        /// Non-throwing parse, used when the connection string comes from user configuration.
        /// </summary>
        public static bool TryCreate(string connectionString, out StorageAccountFactory factory)
        {
            try
            {
                factory = new StorageAccountFactory(connectionString);
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }

            factory = null;
            return false;
        }

        /// <summary>
        /// Builds an account from an explicit account name plus key, bypassing the connection
        /// string format entirely.
        /// </summary>
        public static StorageAccountFactory FromSharedKey(string accountName, string accountKey)
        {
            return new StorageAccountFactory(
                accountName,
                new StorageSharedKeyCredential(accountName, accountKey),
                new TableSharedKeyCredential(accountName, accountKey),
                sasCredential: null);
        }

        /// <summary>
        /// Builds an account from a pre-issued SAS token.
        /// </summary>
        public static StorageAccountFactory FromSasToken(string accountName, string sasToken)
        {
            return new StorageAccountFactory(
                accountName,
                sharedKeyCredential: null,
                tableSharedKeyCredential: null,
                new AzureSasCredential(sasToken.TrimStart('?')));
        }

        public BlobServiceClient CreateBlobClient()
        {
            var options = new BlobClientOptions();
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = TimeSpan.FromSeconds(3);
            options.Retry.MaxRetries = 4;
            options.Retry.NetworkTimeout = TimeSpan.FromMinutes(5);

            if (_connectionString != null)
            {
                return new BlobServiceClient(_connectionString, options);
            }

            return _sharedKeyCredential != null
                ? new BlobServiceClient(ServiceUri("blob"), _sharedKeyCredential, options)
                : new BlobServiceClient(ServiceUri("blob"), _sasCredential, options);
        }

        public QueueServiceClient CreateQueueClient()
        {
            var options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.None,
            };
            options.Retry.Mode = RetryMode.Fixed;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.MaxRetries = 3;

            if (_connectionString != null)
            {
                return new QueueServiceClient(_connectionString, options);
            }

            return _sharedKeyCredential != null
                ? new QueueServiceClient(ServiceUri("queue"), _sharedKeyCredential, options)
                : new QueueServiceClient(ServiceUri("queue"), _sasCredential, options);
        }

        public TableServiceClient CreateTableClient()
        {
            if (_connectionString != null)
            {
                return new TableServiceClient(_connectionString);
            }

            return _tableSharedKeyCredential != null
                ? new TableServiceClient(ServiceUri("table"), _tableSharedKeyCredential)
                : new TableServiceClient(ServiceUri("table"), _sasCredential);
        }

        public ShareServiceClient CreateFileClient()
        {
            if (_connectionString != null)
            {
                return new ShareServiceClient(_connectionString);
            }

            return _sharedKeyCredential != null
                ? new ShareServiceClient(ServiceUri("file"), _sharedKeyCredential)
                : new ShareServiceClient(ServiceUri("file"), _sasCredential);
        }

        public Uri BlobEndpoint
        {
            get { return CreateBlobClient().Uri; }
        }

        private Uri ServiceUri(string service)
        {
            return new Uri($"https://{_accountName}.{service}.core.windows.net");
        }
    }
}

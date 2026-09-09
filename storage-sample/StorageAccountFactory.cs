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
        private readonly BlobServiceClient _blobClient;
        private readonly QueueServiceClient _queueClient;
        private readonly TableServiceClient _tableClient;
        private readonly ShareServiceClient _fileClient;

        public StorageAccountFactory(string connectionString)
        {
            _blobClient = new BlobServiceClient(connectionString, CreateBlobOptions());
            _queueClient = new QueueServiceClient(connectionString, CreateQueueOptions());
            _tableClient = new TableServiceClient(connectionString);
            _fileClient = new ShareServiceClient(connectionString);
        }

        private StorageAccountFactory(
            BlobServiceClient blobClient,
            QueueServiceClient queueClient,
            TableServiceClient tableClient,
            ShareServiceClient fileClient)
        {
            _blobClient = blobClient;
            _queueClient = queueClient;
            _tableClient = tableClient;
            _fileClient = fileClient;
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
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                factory = null;
                return false;
            }
        }

        /// <summary>
        /// Builds an account from an explicit account name plus key, bypassing the connection
        /// string format entirely.
        /// </summary>
        public static StorageAccountFactory FromSharedKey(string accountName, string accountKey)
        {
            var storageCredential = new StorageSharedKeyCredential(accountName, accountKey);
            var tableCredential = new TableSharedKeyCredential(accountName, accountKey);

            return new StorageAccountFactory(
                new BlobServiceClient(CreateServiceUri(accountName, "blob"), storageCredential, CreateBlobOptions()),
                new QueueServiceClient(CreateServiceUri(accountName, "queue"), storageCredential, CreateQueueOptions()),
                new TableServiceClient(CreateServiceUri(accountName, "table"), tableCredential),
                new ShareServiceClient(CreateServiceUri(accountName, "file"), storageCredential));
        }

        /// <summary>
        /// Builds an account from a pre-issued SAS token.
        /// </summary>
        public static StorageAccountFactory FromSasToken(string accountName, string sasToken)
        {
            var credential = new AzureSasCredential(sasToken?.TrimStart('?'));

            return new StorageAccountFactory(
                new BlobServiceClient(CreateServiceUri(accountName, "blob"), credential, CreateBlobOptions()),
                new QueueServiceClient(CreateServiceUri(accountName, "queue"), credential, CreateQueueOptions()),
                new TableServiceClient(CreateServiceUri(accountName, "table"), credential),
                new ShareServiceClient(CreateServiceUri(accountName, "file"), credential));
        }

        public BlobServiceClient CreateBlobClient()
        {
            return _blobClient;
        }

        public QueueServiceClient CreateQueueClient()
        {
            return _queueClient;
        }

        public TableServiceClient CreateTableClient()
        {
            return _tableClient;
        }

        public ShareServiceClient CreateFileClient()
        {
            return _fileClient;
        }

        public Uri BlobEndpoint
        {
            get { return _blobClient.Uri; }
        }

        private static BlobClientOptions CreateBlobOptions()
        {
            var options = new BlobClientOptions();
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = TimeSpan.FromSeconds(3);
            options.Retry.MaxRetries = 4;
            options.Retry.NetworkTimeout = TimeSpan.FromMinutes(5);
            return options;
        }

        private static QueueClientOptions CreateQueueOptions()
        {
            var options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64,
            };

            options.Retry.Mode = RetryMode.Fixed;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.MaxRetries = 3;
            return options;
        }

        private static Uri CreateServiceUri(string accountName, string service)
        {
            return new Uri($"https://{accountName}.{service}.core.windows.net");
        }
    }
}

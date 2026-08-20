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
    /// Single entry point for the service-specific Azure Storage clients.
    /// </summary>
    public class StorageAccountFactory
    {
        internal static readonly TimeSpan BlobOperationTimeout = TimeSpan.FromMinutes(5);
        internal const int BlobTransferConcurrency = 4;

        private readonly BlobServiceClient _blobs;
        private readonly QueueServiceClient _queues;
        private readonly TableServiceClient _tables;
        private readonly ShareServiceClient _files;

        public StorageAccountFactory(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);

            _blobs = new BlobServiceClient(connectionString, CreateBlobOptions());
            _queues = new QueueServiceClient(connectionString, CreateQueueOptions());
            _tables = new TableServiceClient(connectionString);
            _files = new ShareServiceClient(connectionString);
        }

        private StorageAccountFactory(
            BlobServiceClient blobs,
            QueueServiceClient queues,
            TableServiceClient tables,
            ShareServiceClient files)
        {
            _blobs = blobs;
            _queues = queues;
            _tables = tables;
            _files = files;
        }

        public static bool TryCreate(string connectionString, out StorageAccountFactory factory)
        {
            try
            {
                factory = new StorageAccountFactory(connectionString);
                return true;
            }
            catch (ArgumentException)
            {
                factory = null;
                return false;
            }
            catch (FormatException)
            {
                factory = null;
                return false;
            }
        }

        public static StorageAccountFactory FromSharedKey(string accountName, string accountKey)
        {
            var storageCredential = new StorageSharedKeyCredential(accountName, accountKey);
            var tableCredential = new TableSharedKeyCredential(accountName, accountKey);
            return new StorageAccountFactory(
                new BlobServiceClient(
                    ServiceUri(accountName, "blob"),
                    storageCredential,
                    CreateBlobOptions()),
                new QueueServiceClient(
                    ServiceUri(accountName, "queue"),
                    storageCredential,
                    CreateQueueOptions()),
                new TableServiceClient(ServiceUri(accountName, "table"), tableCredential),
                new ShareServiceClient(ServiceUri(accountName, "file"), storageCredential));
        }

        public static StorageAccountFactory FromSasToken(string accountName, string sasToken)
        {
            var credential = new AzureSasCredential(sasToken);
            return new StorageAccountFactory(
                new BlobServiceClient(ServiceUri(accountName, "blob"), credential, CreateBlobOptions()),
                new QueueServiceClient(ServiceUri(accountName, "queue"), credential, CreateQueueOptions()),
                new TableServiceClient(ServiceUri(accountName, "table"), credential),
                new ShareServiceClient(ServiceUri(accountName, "file"), credential));
        }

        public BlobServiceClient CreateBlobClient() => _blobs;

        public QueueServiceClient CreateQueueClient() => _queues;

        public TableServiceClient CreateTableClient() => _tables;

        public ShareServiceClient CreateFileClient() => _files;

        public Uri BlobEndpoint => _blobs.Uri;

        private static Uri ServiceUri(string accountName, string service) =>
            new($"https://{accountName}.{service}.core.windows.net/");

        internal static BlobClientOptions CreateBlobOptions()
        {
            var options = new BlobClientOptions();
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = TimeSpan.FromSeconds(3);
            options.Retry.MaxRetries = 4;
            return options;
        }

        internal static QueueClientOptions CreateQueueOptions()
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
    }
}

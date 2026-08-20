using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.File;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace Contoso.Documents
{
    /// <summary>
    /// Single entry point for every storage service. <see cref="CloudStorageAccount"/> is the
    /// legacy façade that hands out per-service clients.
    /// </summary>
    public class StorageAccountFactory
    {
        private readonly CloudStorageAccount _account;

        public StorageAccountFactory(string connectionString)
        {
            _account = CloudStorageAccount.Parse(connectionString);
        }

        private StorageAccountFactory(CloudStorageAccount account)
        {
            _account = account;
        }

        /// <summary>
        /// Non-throwing parse, used when the connection string comes from user configuration.
        /// </summary>
        public static bool TryCreate(string connectionString, out StorageAccountFactory factory)
        {
            CloudStorageAccount account;

            if (CloudStorageAccount.TryParse(connectionString, out account))
            {
                factory = new StorageAccountFactory(account);
                return true;
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
            var credentials = new StorageCredentials(accountName, accountKey);
            var account = new CloudStorageAccount(credentials, useHttps: true);

            return new StorageAccountFactory(account);
        }

        /// <summary>
        /// Builds an account from a pre-issued SAS token.
        /// </summary>
        public static StorageAccountFactory FromSasToken(string accountName, string sasToken)
        {
            var credentials = new StorageCredentials(sasToken);
            var account = new CloudStorageAccount(credentials, accountName, endpointSuffix: null, useHttps: true);

            return new StorageAccountFactory(account);
        }

        public CloudBlobClient CreateBlobClient()
        {
            CloudBlobClient client = _account.CreateCloudBlobClient();

            client.DefaultRequestOptions = new BlobRequestOptions
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(3), 4),
                MaximumExecutionTime = TimeSpan.FromMinutes(5),
                ParallelOperationThreadCount = 4,
            };

            return client;
        }

        public CloudQueueClient CreateQueueClient()
        {
            CloudQueueClient client = _account.CreateCloudQueueClient();

            client.DefaultRequestOptions = new QueueRequestOptions
            {
                RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(2), 3),
            };

            return client;
        }

        public CloudTableClient CreateTableClient()
        {
            return _account.CreateCloudTableClient();
        }

        public CloudFileClient CreateFileClient()
        {
            return _account.CreateCloudFileClient();
        }

        public Uri BlobEndpoint
        {
            get { return _account.BlobEndpoint; }
        }
    }
}

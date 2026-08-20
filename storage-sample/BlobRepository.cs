using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Contoso.Documents
{
    /// <summary>
    /// Stores customer documents as block blobs and keeps a per-customer audit trail in an
    /// append blob.
    /// </summary>
    public class BlobRepository
    {
        private readonly CloudBlobContainer _container;

        public BlobRepository(CloudBlobClient client, string containerName)
        {
            _container = client.GetContainerReference(containerName);
        }

        public async Task InitializeAsync()
        {
            await _container.CreateIfNotExistsAsync().ConfigureAwait(false);

            await _container
                .SetPermissionsAsync(new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Off,
                })
                .ConfigureAwait(false);
        }

        public async Task UploadAsync(string blobName, Stream content, string contentType)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);

            blob.Properties.ContentType = contentType;
            blob.Metadata["uploaded-by"] = "contoso-documents";
            blob.Metadata["uploaded-on"] = DateTime.UtcNow.ToString("O");

            await blob.UploadFromStreamAsync(content).ConfigureAwait(false);
            await blob.SetMetadataAsync().ConfigureAwait(false);
        }

        public async Task UploadTextAsync(string blobName, string text)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            await blob.UploadTextAsync(text).ConfigureAwait(false);
        }

        public async Task<Stream> DownloadAsync(string blobName)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);

            var buffer = new MemoryStream();
            await blob.DownloadToStreamAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;

            return buffer;
        }

        public async Task<string> DownloadTextAsync(string blobName)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            return await blob.DownloadTextAsync().ConfigureAwait(false);
        }

        public async Task<bool> ExistsAsync(string blobName)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            return await blob.ExistsAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(string blobName)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            return await blob.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Segmented listing. Every legacy listing loop looks like this: an opaque
        /// continuation token threaded through a do/while.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListAsync(string prefix)
        {
            var names = new List<string>();
            BlobContinuationToken token = null;

            do
            {
                BlobResultSegment segment = await _container
                    .ListBlobsSegmentedAsync(
                        prefix,
                        useFlatBlobListing: true,
                        blobListingDetails: BlobListingDetails.Metadata,
                        maxResults: 100,
                        currentToken: token,
                        options: null,
                        operationContext: null)
                    .ConfigureAwait(false);

                foreach (IListBlobItem item in segment.Results)
                {
                    var blob = item as CloudBlockBlob;
                    if (blob != null)
                    {
                        names.Add(blob.Name);
                    }
                }

                token = segment.ContinuationToken;
            }
            while (token != null);

            return names;
        }

        /// <summary>
        /// Appends a line to the customer's audit blob, creating it on first use.
        /// </summary>
        public async Task AppendAuditLineAsync(string customerId, string line)
        {
            CloudAppendBlob blob = _container.GetAppendBlobReference($"audit/{customerId}.log");

            if (!await blob.ExistsAsync().ConfigureAwait(false))
            {
                await blob.CreateOrReplaceAsync().ConfigureAwait(false);
            }

            await blob.AppendTextAsync($"{DateTime.UtcNow:O} {line}{Environment.NewLine}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Server-side copy between two blobs in the same container.
        /// </summary>
        public async Task CopyAsync(string sourceName, string destinationName)
        {
            CloudBlockBlob source = _container.GetBlockBlobReference(sourceName);
            CloudBlockBlob destination = _container.GetBlockBlobReference(destinationName);

            await destination.StartCopyAsync(source).ConfigureAwait(false);
        }

        /// <summary>
        /// Issues a short-lived read SAS so a browser can download the document directly.
        /// </summary>
        public string GetReadSasUri(string blobName, TimeSpan lifetime)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);

            var policy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.Add(lifetime),
            };

            string sas = blob.GetSharedAccessSignature(policy);
            return blob.Uri + sas;
        }

        /// <summary>
        /// Optimistic concurrency using the legacy <see cref="AccessCondition"/> type.
        /// </summary>
        public async Task<bool> TryUpdateIfUnchangedAsync(string blobName, string text, string etag)
        {
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);

            try
            {
                await blob
                    .UploadTextAsync(text, null, AccessCondition.GenerateIfMatchCondition(etag), null, null)
                    .ConfigureAwait(false);

                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 412)
            {
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace Contoso.Documents
{
    /// <summary>
    /// Stores customer documents as block blobs and keeps a per-customer audit trail in an
    /// append blob.
    /// </summary>
    public class BlobRepository
    {
        private readonly BlobContainerClient _container;

        public BlobRepository(BlobServiceClient client, string containerName)
        {
            _container = client.GetBlobContainerClient(containerName);
        }

        public async Task InitializeAsync()
        {
            await _container.CreateIfNotExistsAsync().ConfigureAwait(false);

            await _container.SetAccessPolicyAsync(PublicAccessType.None).ConfigureAwait(false);
        }

        public async Task UploadAsync(string blobName, Stream content, string contentType)
        {
            BlockBlobClient blob = _container.GetBlockBlobClient(blobName);
            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Metadata = new Dictionary<string, string>
                {
                    ["uploaded-by"] = "contoso-documents",
                    ["uploaded-on"] = DateTime.UtcNow.ToString("O"),
                },
                TransferOptions = CreateTransferOptions(),
            };

            await blob.UploadAsync(content, options).ConfigureAwait(false);
        }

        public async Task UploadTextAsync(string blobName, string text)
        {
            BlockBlobClient blob = _container.GetBlockBlobClient(blobName);
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
            await blob
                .UploadAsync(
                    content,
                    new BlobUploadOptions { TransferOptions = CreateTransferOptions() })
                .ConfigureAwait(false);
        }

        public async Task<Stream> DownloadAsync(string blobName)
        {
            BlobClient blob = _container.GetBlobClient(blobName);

            var buffer = new MemoryStream();
            await blob.DownloadToAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;

            return buffer;
        }

        public async Task<string> DownloadTextAsync(string blobName)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            BlobDownloadResult result = await blob.DownloadContentAsync().ConfigureAwait(false);
            return result.Content.ToString();
        }

        public async Task<bool> ExistsAsync(string blobName)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            return (await blob.ExistsAsync().ConfigureAwait(false)).Value;
        }

        public async Task<bool> DeleteAsync(string blobName)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            return (await blob.DeleteIfExistsAsync().ConfigureAwait(false)).Value;
        }

        /// <summary>
        /// Segmented listing. Every legacy listing loop looks like this: an opaque
        /// continuation token threaded through a do/while.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListAsync(string prefix)
        {
            var names = new List<string>();
            await foreach (BlobItem item in _container
                .GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, CancellationToken.None)
                .ConfigureAwait(false))
            {
                if (item.Properties.BlobType == BlobType.Block)
                {
                    names.Add(item.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Appends a line to the customer's audit blob, creating it on first use.
        /// </summary>
        public async Task AppendAuditLineAsync(string customerId, string line)
        {
            AppendBlobClient blob = _container.GetAppendBlobClient($"audit/{customerId}.log");
            await blob.CreateIfNotExistsAsync().ConfigureAwait(false);

            byte[] entry = Encoding.UTF8.GetBytes(
                $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
            using var content = new MemoryStream(entry);
            await blob.AppendBlockAsync(content).ConfigureAwait(false);
        }

        /// <summary>
        /// Server-side copy between two blobs in the same container.
        /// </summary>
        public async Task CopyAsync(string sourceName, string destinationName)
        {
            BlobClient source = _container.GetBlobClient(sourceName);
            BlobClient destination = _container.GetBlobClient(destinationName);

            await destination.StartCopyFromUriAsync(source.Uri).ConfigureAwait(false);
        }

        /// <summary>
        /// Issues a short-lived read SAS so a browser can download the document directly.
        /// </summary>
        public string GetReadSasUri(string blobName, TimeSpan lifetime)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            var sas = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime),
            };
            sas.SetPermissions(BlobSasPermissions.Read);

            return blob.GenerateSasUri(sas).AbsoluteUri;
        }

        /// <summary>
        /// Optimistic concurrency using the legacy <see cref="AccessCondition"/> type.
        /// </summary>
        public async Task<bool> TryUpdateIfUnchangedAsync(string blobName, string text, string etag)
        {
            BlockBlobClient blob = _container.GetBlockBlobClient(blobName);

            try
            {
                var options = new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
                    TransferOptions = CreateTransferOptions(),
                };
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
                await blob.UploadAsync(content, options).ConfigureAwait(false);

                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return false;
            }
        }

        private static StorageTransferOptions CreateTransferOptions()
        {
            return new StorageTransferOptions { MaximumConcurrency = 4 };
        }
    }
}

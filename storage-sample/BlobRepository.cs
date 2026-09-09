using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
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
        private static readonly TimeSpan MaximumExecutionTime = TimeSpan.FromMinutes(5);

        private readonly BlobContainerClient _container;

        public BlobRepository(BlobServiceClient client, string containerName)
        {
            _container = client.GetBlobContainerClient(containerName);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);

            await _container
                .CreateIfNotExistsAsync(cancellationToken: operation.Token)
                .ConfigureAwait(false);

            await _container
                .SetAccessPolicyAsync(
                    PublicAccessType.None,
                    cancellationToken: operation.Token)
                .ConfigureAwait(false);
        }

        public async Task UploadAsync(
            string blobName,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);

            await blob.UploadAsync(
                    content,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                        Metadata = new Dictionary<string, string>
                        {
                            ["uploaded-by"] = "contoso-documents",
                            ["uploaded-on"] = DateTime.UtcNow.ToString("O"),
                        },
                        TransferOptions = new Azure.Storage.StorageTransferOptions
                        {
                            MaximumConcurrency = 4,
                        },
                    },
                    operation.Token)
                .ConfigureAwait(false);
        }

        public async Task UploadTextAsync(
            string blobName,
            string text,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);
            await blob
                .UploadAsync(
                    BinaryData.FromString(text),
                    overwrite: true,
                    operation.Token)
                .ConfigureAwait(false);
        }

        public async Task<Stream> DownloadAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);

            var buffer = new MemoryStream();
            await blob.DownloadToAsync(buffer, operation.Token).ConfigureAwait(false);
            buffer.Position = 0;

            return buffer;
        }

        public async Task<string> DownloadTextAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);
            BlobDownloadResult result = await blob
                .DownloadContentAsync(operation.Token)
                .ConfigureAwait(false);
            return result.Content.ToString();
        }

        public async Task<bool> ExistsAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);
            return (await blob.ExistsAsync(operation.Token).ConfigureAwait(false)).Value;
        }

        public async Task<bool> DeleteAsync(
            string blobName,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);
            return (await blob
                .DeleteIfExistsAsync(cancellationToken: operation.Token)
                .ConfigureAwait(false)).Value;
        }

        /// <summary>
        /// Asynchronously lists every matching blob while retaining the legacy page-size hint.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListAsync(
            string prefix,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            var names = new List<string>();
            await foreach (Page<BlobItem> page in _container
                .GetBlobsAsync(
                    BlobTraits.Metadata,
                    BlobStates.None,
                    prefix,
                    operation.Token)
                .AsPages(pageSizeHint: 100))
            {
                foreach (BlobItem item in page.Values)
                {
                    if (item.Properties.BlobType == BlobType.Block)
                    {
                        names.Add(item.Name);
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Appends a line to the customer's audit blob, creating it on first use.
        /// </summary>
        public async Task AppendAuditLineAsync(
            string customerId,
            string line,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            AppendBlobClient blob = _container.GetAppendBlobClient($"audit/{customerId}.log");
            await blob
                .CreateIfNotExistsAsync(cancellationToken: operation.Token)
                .ConfigureAwait(false);

            byte[] content = Encoding.UTF8.GetBytes(
                $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
            using var stream = new MemoryStream(content, writable: false);
            await blob.AppendBlockAsync(stream, cancellationToken: operation.Token)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Server-side copy between two blobs in the same container.
        /// </summary>
        public async Task CopyAsync(
            string sourceName,
            string destinationName,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient source = _container.GetBlobClient(sourceName);
            BlobClient destination = _container.GetBlobClient(destinationName);

            await destination
                .StartCopyFromUriAsync(source.Uri, cancellationToken: operation.Token)
                .ConfigureAwait(false);
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

            return blob.GenerateSasUri(sas).ToString();
        }

        /// <summary>
        /// Optimistic concurrency using an If-Match ETag condition.
        /// </summary>
        public async Task<bool> TryUpdateIfUnchangedAsync(
            string blobName,
            string text,
            string etag,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource operation =
                CreateOperationCancellation(cancellationToken);
            BlobClient blob = _container.GetBlobClient(blobName);

            try
            {
                await blob
                    .UploadAsync(
                        BinaryData.FromString(text),
                        new BlobUploadOptions
                        {
                            Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
                        },
                        operation.Token)
                    .ConfigureAwait(false);

                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return false;
            }
        }

        private static CancellationTokenSource CreateOperationCancellation(
            CancellationToken cancellationToken)
        {
            CancellationTokenSource operation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operation.CancelAfter(MaximumExecutionTime);
            return operation;
        }
    }
}

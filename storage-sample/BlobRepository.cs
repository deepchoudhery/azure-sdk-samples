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
    public class BlobRepository
    {
        private readonly BlobContainerClient _container;

        public BlobRepository(BlobServiceClient client, string containerName)
        {
            _container = client.GetBlobContainerClient(containerName);
        }

        public async Task InitializeAsync()
        {
            using var timeout = CreateOperationTimeout();
            await _container.CreateIfNotExistsAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            await _container.SetAccessPolicyAsync(
                    PublicAccessType.None,
                    cancellationToken: timeout.Token)
                .ConfigureAwait(false);
        }

        public async Task UploadAsync(string blobName, Stream content, string contentType)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                TransferOptions = CreateTransferOptions(),
                Metadata = new Dictionary<string, string>
                {
                    ["uploaded-by"] = "contoso-documents",
                    ["uploaded-on"] = DateTime.UtcNow.ToString("O"),
                },
            };
            using var timeout = CreateOperationTimeout();
            await blob.UploadAsync(content, options, timeout.Token).ConfigureAwait(false);
        }

        public async Task UploadTextAsync(string blobName, string text)
        {
            var options = new BlobUploadOptions
            {
                TransferOptions = CreateTransferOptions(),
            };
            using var timeout = CreateOperationTimeout();
            await _container.GetBlobClient(blobName)
                .UploadAsync(BinaryData.FromString(text), options, timeout.Token)
                .ConfigureAwait(false);
        }

        public async Task<Stream> DownloadAsync(string blobName)
        {
            var buffer = new MemoryStream();
            var options = new BlobDownloadToOptions
            {
                TransferOptions = CreateTransferOptions(),
            };
            using var timeout = CreateOperationTimeout();
            await _container.GetBlobClient(blobName)
                .DownloadToAsync(buffer, options, timeout.Token)
                .ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }

        public async Task<string> DownloadTextAsync(string blobName)
        {
            using var buffer = new MemoryStream();
            var options = new BlobDownloadToOptions
            {
                TransferOptions = CreateTransferOptions(),
            };
            using var timeout = CreateOperationTimeout();
            await _container.GetBlobClient(blobName)
                .DownloadToAsync(buffer, options, timeout.Token)
                .ConfigureAwait(false);
            buffer.Position = 0;
            using var reader = new StreamReader(
                buffer,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        public async Task<bool> ExistsAsync(string blobName)
        {
            using var timeout = CreateOperationTimeout();
            return (await _container.GetBlobClient(blobName)
                .ExistsAsync(timeout.Token)
                .ConfigureAwait(false)).Value;
        }

        public async Task<bool> DeleteAsync(string blobName)
        {
            using var timeout = CreateOperationTimeout();
            return (await _container.GetBlobClient(blobName)
                .DeleteIfExistsAsync(cancellationToken: timeout.Token)
                .ConfigureAwait(false)).Value;
        }

        public async Task<IReadOnlyList<string>> ListAsync(string prefix)
        {
            var names = new List<string>();
            using var timeout = CreateOperationTimeout();
            await foreach (BlobItem item in _container.GetBlobsAsync(
                BlobTraits.Metadata,
                BlobStates.None,
                prefix,
                timeout.Token))
            {
                if (IsBlockBlob(item.Properties.BlobType))
                {
                    names.Add(item.Name);
                }
            }
            return names;
        }

        public async Task AppendAuditLineAsync(string customerId, string line)
        {
            AppendBlobClient blob = _container.GetAppendBlobClient($"audit/{customerId}.log");
            using var timeout = CreateOperationTimeout();
            await blob.CreateIfNotExistsAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            using var content =
                new MemoryStream(Encoding.UTF8.GetBytes($"{DateTime.UtcNow:O} {line}{Environment.NewLine}"));
            await blob.AppendBlockAsync(content, cancellationToken: timeout.Token).ConfigureAwait(false);
        }

        public async Task CopyAsync(string sourceName, string destinationName)
        {
            BlobClient source = _container.GetBlobClient(sourceName);
            BlobClient destination = _container.GetBlobClient(destinationName);
            using var timeout = CreateOperationTimeout();
            await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: timeout.Token)
                .ConfigureAwait(false);
        }

        public string GetReadSasUri(string blobName, TimeSpan lifetime)
        {
            BlobClient blob = _container.GetBlobClient(blobName);
            var builder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime),
            };
            builder.SetPermissions(BlobSasPermissions.Read);
            return blob.GenerateSasUri(builder).AbsoluteUri;
        }

        public async Task<bool> TryUpdateIfUnchangedAsync(string blobName, string text, string etag)
        {
            try
            {
                using var timeout = CreateOperationTimeout();
                await _container.GetBlobClient(blobName)
                    .UploadAsync(
                        BinaryData.FromString(text),
                        new BlobUploadOptions
                        {
                            Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
                            TransferOptions = CreateTransferOptions(),
                        },
                        timeout.Token)
                    .ConfigureAwait(false);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return false;
            }
        }

        internal static bool IsBlockBlob(BlobType? blobType) => blobType == BlobType.Block;

        internal static StorageTransferOptions CreateTransferOptions() =>
            new() { MaximumConcurrency = StorageAccountFactory.BlobTransferConcurrency };

        private static CancellationTokenSource CreateOperationTimeout() =>
            new(StorageAccountFactory.BlobOperationTimeout);
    }
}

using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Contoso.Documents;
using Moq;
using Xunit;

namespace Contoso.Documents.Tests;

public class StorageCompatibilityTests
{
    [Fact]
    public void QueueOptions_PreserveLegacyBase64WireEncoding()
    {
        Assert.Equal(
            QueueMessageEncoding.Base64,
            StorageAccountFactory.CreateQueueOptions().MessageEncoding);
    }

    [Fact]
    public void BlobPolicy_PreservesLegacyOverallTimeoutAndConcurrency()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), StorageAccountFactory.BlobOperationTimeout);

        StorageTransferOptions transferOptions = BlobRepository.CreateTransferOptions();
        Assert.Equal(4, transferOptions.MaximumConcurrency);
    }

    [Fact]
    public async Task DownloadTextAsync_UsesBoundedTransferAndRemovesUtf8Bom()
    {
        const string blobName = "documents/résumé.txt";
        const string expected = "résumé 東京 🚀";
        var service = new Mock<BlobServiceClient>(MockBehavior.Strict);
        var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        BlobDownloadToOptions? capturedOptions = null;

        service.Setup(client => client.GetBlobContainerClient("documents")).Returns(container.Object);
        container.Setup(client => client.GetBlobClient(blobName)).Returns(blob.Object);
        blob.Setup(client => client.DownloadToAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobDownloadToOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobDownloadToOptions, CancellationToken>((destination, options, _) =>
            {
                capturedOptions = options;
                destination.Write(System.Text.Encoding.UTF8.Preamble);
                destination.Write(System.Text.Encoding.UTF8.GetBytes(expected));
            })
            .ReturnsAsync(Mock.Of<Response>());

        string actual = await new BlobRepository(service.Object, "documents")
            .DownloadTextAsync(blobName);

        Assert.Equal(expected, actual);
        Assert.NotNull(capturedOptions);
        Assert.Equal(4, capturedOptions.TransferOptions.MaximumConcurrency);
        blob.Verify(client => client.DownloadToAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobDownloadToOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TableListing_StopsAfterLegacyTotalLimit()
    {
        DocumentEntity[] entities = Enumerable.Range(0, 250)
            .Select(index => new DocumentEntity
            {
                PartitionKey = "customer",
                RowKey = index.ToString(),
            })
            .ToArray();
        AsyncPageable<DocumentEntity> pageable = AsyncPageable<DocumentEntity>.FromPages(
            new[]
            {
                Page<DocumentEntity>.FromValues(entities, null, Mock.Of<Response>()),
            });

        IReadOnlyList<DocumentEntity> result =
            await TableRepository.CollectAtMostAsync(pageable, 200);

        Assert.Equal(200, result.Count);
        Assert.Equal("199", result[^1].RowKey);
    }

    [Theory]
    [InlineData(BlobType.Block, true)]
    [InlineData(BlobType.Append, false)]
    [InlineData(BlobType.Page, false)]
    public void BlobListing_IncludesOnlyLegacyBlockBlobType(BlobType blobType, bool expected)
    {
        Assert.Equal(expected, BlobRepository.IsBlockBlob(blobType));
    }

    [Fact]
    public void TableDump_ReturnsOnlyCustomProperties()
    {
        var properties = new Dictionary<string, object>
        {
            [nameof(ITableEntity.PartitionKey)] = "customer",
            [nameof(ITableEntity.RowKey)] = "document",
            [nameof(ITableEntity.Timestamp)] = DateTimeOffset.UtcNow,
            [nameof(ITableEntity.ETag)] = new Azure.ETag("etag"),
            ["Title"] = "Quarterly report",
            ["IsArchived"] = false,
        };

        IDictionary<string, object> result = TableRepository.GetCustomProperties(properties);

        Assert.Equal(2, result.Count);
        Assert.Equal("Quarterly report", result["Title"]);
        Assert.Equal(false, result["IsArchived"]);
    }
}

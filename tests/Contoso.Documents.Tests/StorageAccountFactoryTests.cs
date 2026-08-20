using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Contoso.Documents;
using Xunit;

namespace Contoso.Documents.Tests;

public class StorageAccountFactoryTests
{
    private const string AccountName = "contosodocs";
    private const string AccountKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private const string CanonicalConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=" + AccountName + ";AccountKey=" + AccountKey;
    private const string FakeSasToken =
        "sv=2018-03-28&ss=b&srt=sco&sp=rl&se=2099-12-31T23%3A59%3A59Z&" +
        "st=2020-01-01T00%3A00%3A00Z&spr=https&sig=ZmFrZXNpZw%3D%3D";

    [Fact]
    public void Constructor_WithCanonicalConnectionString_ExposesServiceEndpoints()
    {
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        Assert.Equal(new Uri("https://contosodocs.blob.core.windows.net/"), factory.BlobEndpoint);
        Assert.Equal(factory.BlobEndpoint, factory.CreateBlobClient().Uri);
        Assert.Equal(new Uri("https://contosodocs.queue.core.windows.net/"), factory.CreateQueueClient().Uri);
        Assert.Equal(new Uri("https://contosodocs.table.core.windows.net/"), factory.CreateTableClient().Uri);
        Assert.Equal(new Uri("https://contosodocs.file.core.windows.net/"), factory.CreateFileClient().Uri);
    }

    [Fact]
    public void Constructor_WithExplicitBlobEndpoint_PreservesEndpoint()
    {
        const string endpoint = "http://127.0.0.1:10000/contosodocs";
        var factory = new StorageAccountFactory(CanonicalConnectionString + ";BlobEndpoint=" + endpoint);

        Assert.Equal(endpoint, factory.BlobEndpoint.OriginalString.TrimEnd('/'));
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => new StorageAccountFactory(null!));
        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithMalformedConnectionString_ThrowsFormatException() =>
        Assert.Throws<FormatException>(() => new StorageAccountFactory("not-a-connection-string"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    public void TryCreate_WithInvalidInput_ReturnsFalse(string? connectionString)
    {
        bool created = StorageAccountFactory.TryCreate(connectionString!, out var factory);
        Assert.False(created);
        Assert.Null(factory);
    }

    [Fact]
    public void FromSharedKey_ProducesClientsThatCanGenerateSas()
    {
        StorageAccountFactory factory = StorageAccountFactory.FromSharedKey(AccountName, AccountKey);

        Assert.True(factory.CreateBlobClient().CanGenerateAccountSasUri);
        Assert.Equal(AccountName, factory.CreateBlobClient().AccountName);
        Assert.Equal(AccountName, factory.CreateQueueClient().AccountName);
        Assert.Equal(AccountName, factory.CreateTableClient().AccountName);
        Assert.Equal(AccountName, factory.CreateFileClient().AccountName);
    }

    [Fact]
    public void FromSasToken_ProducesCanonicalServiceClients()
    {
        StorageAccountFactory factory = StorageAccountFactory.FromSasToken(AccountName, FakeSasToken);

        Assert.False(factory.CreateBlobClient().CanGenerateAccountSasUri);
        Assert.Equal(new Uri("https://contosodocs.blob.core.windows.net/"), factory.BlobEndpoint);
        Assert.Equal(new Uri("https://contosodocs.queue.core.windows.net/"), factory.CreateQueueClient().Uri);
    }

    [Fact]
    public void Factory_ReturnsModernServiceSpecificClientTypes()
    {
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        Assert.IsType<BlobServiceClient>(factory.CreateBlobClient());
        Assert.IsType<QueueServiceClient>(factory.CreateQueueClient());
        Assert.IsType<ShareServiceClient>(factory.CreateFileClient());
    }
}

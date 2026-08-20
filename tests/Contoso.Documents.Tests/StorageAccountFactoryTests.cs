using Xunit;
using Contoso.Documents;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace Contoso.Documents.Tests;

public class StorageAccountFactoryTests
{
    private const string AccountName = "contosodocs";
    private const string AccountKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private const string CanonicalConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=" + AccountName + ";AccountKey=" + AccountKey;
    private const string CanonicalBlobEndpoint = "https://contosodocs.blob.core.windows.net/";
    private const string FakeSasToken =
        "sv=2018-03-28&ss=b&srt=sco&sp=rl&se=2099-12-31T23%3A59%3A59Z&" +
        "st=2020-01-01T00%3A00%3A00Z&spr=https&sig=ZmFrZXNpZw%3D%3D";

    [Fact]
    public void Constructor_WithCanonicalConnectionString_ExposesCanonicalBlobEndpoint()
    {
        // Act
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        // Assert
        AssertCanonicalBlobEndpoint(factory);
    }

    [Fact]
    public void Constructor_WithExplicitBlobEndpoint_PreservesEndpointText()
    {
        // Arrange
        const string explicitEndpoint = "http://127.0.0.1:10000/contosodocs";
        string connectionString = CanonicalConnectionString + ";BlobEndpoint=" + explicitEndpoint;

        // Act
        var factory = new StorageAccountFactory(connectionString);

        // Assert
        Uri endpoint = Assert.IsType<Uri>(factory.BlobEndpoint);
        Assert.Equal(new Uri(explicitEndpoint), endpoint);
        Assert.Equal(explicitEndpoint, endpoint.OriginalString);
        Assert.Equal(explicitEndpoint, endpoint.AbsoluteUri);
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new StorageAccountFactory(null!));

        // Assert
        Assert.Equal("connectionString", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithMalformedConnectionString_ThrowsFormatException()
    {
        // Act and assert
        Assert.Throws<FormatException>(
            () => new StorageAccountFactory("not-a-valid-storage-connection-string"));
    }

    [Fact]
    public void TryCreate_WithCanonicalConnectionString_ReturnsFactoryAndExactEndpoint()
    {
        // Act
        bool created = StorageAccountFactory.TryCreate(CanonicalConnectionString, out StorageAccountFactory factory);

        // Assert
        Assert.True(created);
        Assert.NotNull(factory);
        AssertCanonicalBlobEndpoint(factory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-storage-connection-string")]
    public void TryCreate_WithInvalidInput_ReturnsFalseAndNullFactory(string? connectionString)
    {
        // Act
        bool created = StorageAccountFactory.TryCreate(
            connectionString!,
            out StorageAccountFactory factory);

        // Assert
        Assert.False(created);
        Assert.Null(factory);
    }

    [Fact]
    public void FromSharedKey_WithFixedCredentials_UsesCanonicalHttpsEndpoint()
    {
        // Act
        StorageAccountFactory factory = StorageAccountFactory.FromSharedKey(AccountName, AccountKey);

        // Assert
        Assert.NotNull(factory);
        AssertCanonicalBlobEndpoint(factory);
        var credentials = factory.CreateBlobClient().Credentials;
        Assert.True(credentials.IsSharedKey);
        Assert.False(credentials.IsSAS);
        Assert.Equal(AccountName, credentials.AccountName);
        Assert.Equal(AccountKey, credentials.ExportBase64EncodedKey());
    }

    [Fact]
    public void FromSasToken_WithFixedFakeToken_UsesCanonicalHttpsEndpoint()
    {
        // Act
        StorageAccountFactory factory = StorageAccountFactory.FromSasToken(AccountName, FakeSasToken);

        // Assert
        Assert.NotNull(factory);
        AssertCanonicalBlobEndpoint(factory);
        var credentials = factory.CreateBlobClient().Credentials;
        Assert.True(credentials.IsSAS);
        Assert.False(credentials.IsSharedKey);
        Assert.Equal(FakeSasToken, credentials.SASToken);
    }

    [Fact]
    public void CreateBlobClient_ConfiguresExecutionParallelismAndRetryPolicy()
    {
        // Arrange
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        // Act
        var client = factory.CreateBlobClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri(CanonicalBlobEndpoint), client.BaseUri);
        Assert.NotNull(client.DefaultRequestOptions);
        Assert.Equal(TimeSpan.FromMinutes(5), client.DefaultRequestOptions.MaximumExecutionTime);
        Assert.Equal(4, client.DefaultRequestOptions.ParallelOperationThreadCount);
        Assert.IsType<ExponentialRetry>(client.DefaultRequestOptions.RetryPolicy);
    }

    [Fact]
    public void CreateBlobClient_ExponentialRetryPinsFirstDelayAndAttemptBoundary()
    {
        // Arrange
        var factory = new StorageAccountFactory(CanonicalConnectionString);
        ExponentialRetry retryPolicy =
            Assert.IsType<ExponentialRetry>(factory.CreateBlobClient().DefaultRequestOptions.RetryPolicy);
        var exception = new InvalidOperationException("fixed storage failure");

        // Act
        bool firstRetry = retryPolicy.ShouldRetry(
            0,
            500,
            exception,
            out TimeSpan firstDelay,
            new OperationContext());
        bool fourthRetry = retryPolicy.ShouldRetry(
            3,
            500,
            exception,
            out _,
            new OperationContext());
        bool fifthRetry = retryPolicy.ShouldRetry(
            4,
            500,
            exception,
            out _,
            new OperationContext());

        // Assert
        Assert.True(firstRetry);
        Assert.Equal(TimeSpan.FromSeconds(3), firstDelay);
        Assert.True(fourthRetry);
        Assert.False(fifthRetry);
    }

    [Fact]
    public void CreateQueueClient_ConfiguresLinearRetry()
    {
        // Arrange
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        // Act
        var client = factory.CreateQueueClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://contosodocs.queue.core.windows.net/"), client.BaseUri);
        Assert.NotNull(client.DefaultRequestOptions);
        LinearRetry retryPolicy =
            Assert.IsType<LinearRetry>(client.DefaultRequestOptions.RetryPolicy);
        var exception = new InvalidOperationException("fixed storage failure");

        bool thirdRetry = retryPolicy.ShouldRetry(
            2,
            500,
            exception,
            out TimeSpan retryDelay,
            new OperationContext());
        bool fourthRetry = retryPolicy.ShouldRetry(
            3,
            500,
            exception,
            out _,
            new OperationContext());

        Assert.True(thirdRetry);
        Assert.Equal(TimeSpan.FromSeconds(2), retryDelay);
        Assert.False(fourthRetry);
    }

    [Fact]
    public void CreateTableClient_ReturnsClientForFactoryAccount()
    {
        // Arrange
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        // Act
        var client = factory.CreateTableClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://contosodocs.table.core.windows.net/"), client.BaseUri);
        Assert.Equal(AccountName, client.Credentials.AccountName);
    }

    [Fact]
    public void CreateFileClient_ReturnsClientForFactoryAccount()
    {
        // Arrange
        var factory = new StorageAccountFactory(CanonicalConnectionString);

        // Act
        var client = factory.CreateFileClient();

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://contosodocs.file.core.windows.net/"), client.BaseUri);
        Assert.Equal(AccountName, client.Credentials.AccountName);
    }

    private static void AssertCanonicalBlobEndpoint(StorageAccountFactory factory)
    {
        Uri endpoint = Assert.IsType<Uri>(factory.BlobEndpoint);

        Assert.Equal(new Uri(CanonicalBlobEndpoint), endpoint);
        Assert.Equal(CanonicalBlobEndpoint, endpoint.OriginalString);
        Assert.Equal(CanonicalBlobEndpoint, endpoint.AbsoluteUri);
    }
}

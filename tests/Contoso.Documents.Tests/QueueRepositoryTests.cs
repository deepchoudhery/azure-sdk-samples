using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Contoso.Documents;
using Moq;
using Xunit;

namespace Contoso.Documents.Tests;

public class QueueRepositoryTests
{
    [Fact]
    public async Task Constructor_WithQueueName_RequestsThatExactQueueClient()
    {
        var service = new Mock<QueueServiceClient>(MockBehavior.Strict);
        var queue = CreateQueue();
        service.Setup(client => client.GetQueueClient("documents-phase3")).Returns(queue.Object);
        queue.Setup(client => client.ClearMessagesAsync(default)).ReturnsAsync(Mock.Of<Response>());

        await new QueueRepository(service.Object, "documents-phase3").ClearAsync();

        service.Verify(client => client.GetQueueClient("documents-phase3"), Times.Once);
        queue.Verify(client => client.ClearMessagesAsync(default), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CreatesQueue()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.CreateIfNotExistsAsync(null, default))
            .ReturnsAsync(Mock.Of<Response>());

        await new QueueRepository(queue.Object).InitializeAsync();

        queue.Verify(q => q.CreateIfNotExistsAsync(null, default), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed create failure");
        var queue = CreateQueue();
        queue.Setup(q => q.CreateIfNotExistsAsync(null, default)).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).InitializeAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task EnqueueAsync_PreservesUnicodePayload()
    {
        const string payload = "résumé 東京 🚀";
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync(payload))
            .ReturnsAsync(Response.FromValue(QueuesModelFactory.SendReceipt("id", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, "receipt", DateTimeOffset.UtcNow), Mock.Of<Response>()));

        await new QueueRepository(queue.Object).EnqueueAsync(payload);

        queue.Verify(q => q.SendMessageAsync(payload), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullPayload_SendsEmptyMessage()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync(string.Empty))
            .ReturnsAsync(Response.FromValue(QueuesModelFactory.SendReceipt("id", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, "receipt", DateTimeOffset.UtcNow), Mock.Of<Response>()));

        await new QueueRepository(queue.Object).EnqueueAsync(null!);

        queue.Verify(q => q.SendMessageAsync(string.Empty), Times.Once);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility()
    {
        TimeSpan delay = TimeSpan.FromSeconds(37);
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync("payload", delay, TimeSpan.FromDays(7), default))
            .ReturnsAsync(Response.FromValue(QueuesModelFactory.SendReceipt("id", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, "receipt", DateTimeOffset.UtcNow), Mock.Of<Response>()));

        await new QueueRepository(queue.Object).EnqueueDelayedAsync("payload", delay);

        queue.Verify(
            q => q.SendMessageAsync("payload", delay, TimeSpan.FromDays(7), default),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_WithNullPayload_SendsEmptyMessage()
    {
        TimeSpan delay = TimeSpan.FromSeconds(19);
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync(string.Empty, delay, TimeSpan.FromDays(7), default))
            .ReturnsAsync(Response.FromValue(QueuesModelFactory.SendReceipt("id", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, "receipt", DateTimeOffset.UtcNow), Mock.Of<Response>()));

        await new QueueRepository(queue.Object).EnqueueDelayedAsync(null!, delay);

        queue.Verify(
            q => q.SendMessageAsync(string.Empty, delay, TimeSpan.FromDays(7), default),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed delayed enqueue failure");
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync(
                "payload", TimeSpan.FromSeconds(23), TimeSpan.FromDays(7), default))
            .ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object)
                .EnqueueDelayedAsync("payload", TimeSpan.FromSeconds(23)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DequeueAsync_WhenEmpty_ReturnsNullWithoutDelete()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessageAsync(null, default))
            .ReturnsAsync(Response.FromValue<QueueMessage>(null!, Mock.Of<Response>()));

        string? payload = await new QueueRepository(queue.Object).DequeueAsync();

        Assert.Null(payload);
        queue.Verify(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DequeueAsync_WhenRetrievalFails_PropagatesWithoutDelete()
    {
        var expected = new InvalidOperationException("fixed dequeue retrieval failure");
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessageAsync(null, default)).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).DequeueAsync());

        Assert.Same(expected, actual);
        queue.Verify(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsPayloadAndDeletesByReceipt()
    {
        QueueMessage message = QueueMessage("message-id", "pop-receipt", "résumé 東京");
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessageAsync(null, default))
            .ReturnsAsync(Response.FromValue(message, Mock.Of<Response>()));
        queue.Setup(q => q.DeleteMessageAsync("message-id", "pop-receipt", default))
            .ReturnsAsync(Mock.Of<Response>());

        string? payload = await new QueueRepository(queue.Object).DequeueAsync();

        Assert.Equal("résumé 東京", payload);
        queue.Verify(q => q.DeleteMessageAsync("message-id", "pop-receipt", default), Times.Once);
    }

    [Fact]
    public async Task DequeueAsync_WhenDeleteFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed dequeue delete failure");
        QueueMessage message = QueueMessage("message-id", "pop-receipt", "payload");
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessageAsync(null, default))
            .ReturnsAsync(Response.FromValue(message, Mock.Of<Response>()));
        queue.Setup(q => q.DeleteMessageAsync("message-id", "pop-receipt", default))
            .ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).DequeueAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DequeueBatchAsync_WhenEmpty_ForwardsCountAndVisibility()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessagesAsync(1, TimeSpan.FromMinutes(2), default))
            .ReturnsAsync(Response.FromValue(Array.Empty<QueueMessage>(), Mock.Of<Response>()));

        IReadOnlyList<string> result = await new QueueRepository(queue.Object).DequeueBatchAsync(1);

        Assert.Empty(result);
        queue.Verify(q => q.ReceiveMessagesAsync(1, TimeSpan.FromMinutes(2), default), Times.Once);
        queue.Verify(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DequeueBatchAsync_PreservesOrderAndVisibilityTimeout()
    {
        QueueMessage[] messages =
        {
            QueueMessage("id-1", "receipt-1", "first"),
            QueueMessage("id-2", "receipt-2", "second"),
        };
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessagesAsync(8, TimeSpan.FromMinutes(2), default))
            .ReturnsAsync(Response.FromValue(messages, Mock.Of<Response>()));
        queue.Setup(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(Mock.Of<Response>());

        IReadOnlyList<string> result = await new QueueRepository(queue.Object).DequeueBatchAsync(8);

        Assert.Equal(new[] { "first", "second" }, result);
        queue.Verify(q => q.DeleteMessageAsync("id-1", "receipt-1", default), Times.Once);
        queue.Verify(q => q.DeleteMessageAsync("id-2", "receipt-2", default), Times.Once);
    }

    [Fact]
    public async Task DequeueBatchAsync_WhenRetrievalFails_PropagatesWithoutDelete()
    {
        var expected = new InvalidOperationException("fixed batch retrieval failure");
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessagesAsync(3, TimeSpan.FromMinutes(2), default))
            .ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).DequeueBatchAsync(3));

        Assert.Same(expected, actual);
        queue.Verify(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DequeueBatchAsync_WhenDeleteFails_StopsProcessing()
    {
        var expected = new InvalidOperationException("fixed batch delete failure");
        QueueMessage[] messages =
        {
            QueueMessage("id-1", "receipt-1", "first"),
            QueueMessage("id-2", "receipt-2", "second"),
        };
        var queue = CreateQueue();
        queue.Setup(q => q.ReceiveMessagesAsync(2, TimeSpan.FromMinutes(2), default))
            .ReturnsAsync(Response.FromValue(messages, Mock.Of<Response>()));
        queue.Setup(q => q.DeleteMessageAsync("id-1", "receipt-1", default))
            .ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).DequeueBatchAsync(2));

        Assert.Same(expected, actual);
        queue.Verify(q => q.DeleteMessageAsync("id-1", "receipt-1", default), Times.Once);
        queue.Verify(q => q.DeleteMessageAsync("id-2", "receipt-2", default), Times.Never);
    }

    [Fact]
    public async Task RenewLeaseAsync_ForwardsIdentityTextAndVisibility()
    {
        QueueMessage message = QueueMessage("id", "receipt", "payload");
        TimeSpan extension = TimeSpan.FromSeconds(83);
        var queue = CreateQueue();
        queue.Setup(q => q.UpdateMessageAsync("id", "receipt", "payload", extension, default))
            .ReturnsAsync(Response.FromValue(
                QueuesModelFactory.UpdateReceipt("receipt-2", DateTimeOffset.UtcNow),
                Mock.Of<Response>()));

        await new QueueRepository(queue.Object).RenewLeaseAsync(message, extension);

        queue.Verify(q => q.UpdateMessageAsync("id", "receipt", "payload", extension, default), Times.Once);
    }

    [Fact]
    public async Task RenewLeaseAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed lease renewal failure");
        QueueMessage message = QueueMessage("id", "receipt", "payload");
        var queue = CreateQueue();
        queue.Setup(q => q.UpdateMessageAsync(
                "id", "receipt", "payload", TimeSpan.FromSeconds(41), default))
            .ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object)
                .RenewLeaseAsync(message, TimeSpan.FromSeconds(41)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task PeekAsync_ReturnsPayloadWithoutDelete()
    {
        PeekedMessage message = QueuesModelFactory.PeekedMessage(
            "id", "payload", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var queue = CreateQueue();
        queue.Setup(q => q.PeekMessageAsync(default))
            .ReturnsAsync(Response.FromValue(message, Mock.Of<Response>()));

        string? payload = await new QueueRepository(queue.Object).PeekAsync();

        Assert.Equal("payload", payload);
        queue.Verify(q => q.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task PeekAsync_WhenEmpty_ReturnsNull()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.PeekMessageAsync(default))
            .ReturnsAsync(Response.FromValue<PeekedMessage>(null!, Mock.Of<Response>()));

        string? payload = await new QueueRepository(queue.Object).PeekAsync();

        Assert.Null(payload);
    }

    [Fact]
    public async Task PeekAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed peek failure");
        var queue = CreateQueue();
        queue.Setup(q => q.PeekMessageAsync(default)).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).PeekAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task GetApproximateLengthAsync_ReturnsServiceCount()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.GetPropertiesAsync(default))
            .ReturnsAsync(Response.FromValue(
                QueuesModelFactory.QueueProperties(new Dictionary<string, string>(), 42),
                Mock.Of<Response>()));

        int count = await new QueueRepository(queue.Object).GetApproximateLengthAsync();

        Assert.Equal(42, count);
    }

    [Fact]
    public async Task GetApproximateLengthAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed properties failure");
        var queue = CreateQueue();
        queue.Setup(q => q.GetPropertiesAsync(default)).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).GetApproximateLengthAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ClearAsync_DelegatesOnce()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.ClearMessagesAsync(default)).ReturnsAsync(Mock.Of<Response>());

        await new QueueRepository(queue.Object).ClearAsync();

        queue.Verify(q => q.ClearMessagesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ClearAsync_WhenSdkFails_PropagatesSameException()
    {
        var expected = new InvalidOperationException("fixed clear failure");
        var queue = CreateQueue();
        queue.Setup(q => q.ClearMessagesAsync(default)).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).ClearAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task SdkFailures_ArePropagated()
    {
        var expected = new InvalidOperationException("fixed failure");
        var queue = CreateQueue();
        queue.Setup(q => q.SendMessageAsync("payload")).ThrowsAsync(expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new QueueRepository(queue.Object).EnqueueAsync("payload"));

        Assert.Same(expected, actual);
    }

    private static Mock<QueueClient> CreateQueue() =>
        new(MockBehavior.Strict);

    private static QueueMessage QueueMessage(string id, string popReceipt, string text) =>
        QueuesModelFactory.QueueMessage(
            id,
            popReceipt,
            text,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}

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
    public async Task InitializeAsync_CreatesQueue()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.CreateIfNotExistsAsync(null, default))
            .ReturnsAsync(Mock.Of<Response>());

        await new QueueRepository(queue.Object).InitializeAsync();

        queue.Verify(q => q.CreateIfNotExistsAsync(null, default), Times.Once);
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
    public async Task ClearAsync_DelegatesOnce()
    {
        var queue = CreateQueue();
        queue.Setup(q => q.ClearMessagesAsync(default)).ReturnsAsync(Mock.Of<Response>());

        await new QueueRepository(queue.Object).ClearAsync();

        queue.Verify(q => q.ClearMessagesAsync(default), Times.Once);
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

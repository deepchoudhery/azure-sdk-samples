using System.Text;
using Contoso.Documents;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Contoso.Documents.Tests;

public class QueueRepositoryTests
{
    [Fact]
    public async Task Constructor_WithQueueName_RequestsThatExactQueueReference()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        var client = new FakeCloudQueueClient(queue);

        // Act
        var repository = new QueueRepository(client, "documents-phase3");
        await repository.ClearAsync();

        // Assert
        Assert.Equal(new[] { "documents-phase3" }, client.RequestedQueueNames);
        Assert.Equal(1, queue.ClearCallCount);
    }

    [Fact]
    public async Task InitializeAsync_CreatesQueueIfMissingOnce()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.InitializeAsync();

        // Assert
        Assert.Equal(1, queue.CreateIfNotExistsCallCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed create failure");
        var queue = new FakeCloudQueue
        {
            CreateIfNotExistsException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(repository.InitializeAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(1, queue.CreateIfNotExistsCallCount);
    }

    [Fact]
    public async Task EnqueueAsync_WithUnicodePayload_PreservesStringAndUtf8Bytes()
    {
        // Arrange
        const string payload = "résumé 東京 🚀";
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.EnqueueAsync(payload);

        // Assert
        CloudQueueMessage message = Assert.Single(queue.AddMessageCalls);
        Assert.Equal(payload, message.AsString);
        Assert.Equal(Encoding.UTF8.GetBytes(payload), message.AsBytes);
        Assert.Empty(queue.AddMessageWithOptionsCalls);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullPayload_CreatesEmptyMessage()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.EnqueueAsync(null!);

        // Assert
        CloudQueueMessage message = Assert.Single(queue.AddMessageCalls);
        Assert.Equal(string.Empty, message.AsString);
        Assert.Empty(message.AsBytes);
        Assert.Empty(queue.AddMessageWithOptionsCalls);
    }

    [Fact]
    public async Task EnqueueAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed enqueue failure");
        var queue = new FakeCloudQueue
        {
            AddMessageException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.EnqueueAsync("payload"));

        // Assert
        Assert.Same(exception, actual);
        Assert.Single(queue.AddMessageCalls);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_ForwardsPayloadTtlAndVisibility()
    {
        // Arrange
        const string payload = "résumé 東京 🚀";
        TimeSpan delay = TimeSpan.FromSeconds(37);
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        Task enqueueTask = repository.EnqueueDelayedAsync(payload, delay);

        // Assert
        Assert.True(enqueueTask.IsCompletedSuccessfully);
        await enqueueTask;
        AddMessageCall call = Assert.Single(queue.AddMessageWithOptionsCalls);
        Assert.Equal(payload, call.Message.AsString);
        Assert.Equal(Encoding.UTF8.GetBytes(payload), call.Message.AsBytes);
        Assert.Equal(TimeSpan.FromDays(7), call.TimeToLive);
        Assert.Equal(delay, call.InitialVisibilityDelay);
        Assert.Null(call.Options);
        Assert.Null(call.Context);
        Assert.Empty(queue.AddMessageCalls);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_WithNullPayload_CreatesEmptyMessage()
    {
        // Arrange
        TimeSpan delay = TimeSpan.FromSeconds(19);
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.EnqueueDelayedAsync(null!, delay);

        // Assert
        AddMessageCall call = Assert.Single(queue.AddMessageWithOptionsCalls);
        Assert.Equal(string.Empty, call.Message.AsString);
        Assert.Empty(call.Message.AsBytes);
        Assert.Equal(TimeSpan.FromDays(7), call.TimeToLive);
        Assert.Equal(delay, call.InitialVisibilityDelay);
    }

    [Fact]
    public async Task EnqueueDelayedAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed delayed enqueue failure");
        var queue = new FakeCloudQueue
        {
            AddMessageWithOptionsException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.EnqueueDelayedAsync("payload", TimeSpan.FromSeconds(23)));

        // Assert
        Assert.Same(exception, actual);
        Assert.Single(queue.AddMessageWithOptionsCalls);
    }

    [Fact]
    public async Task DequeueAsync_WhenQueueIsEmpty_ReturnsNullWithoutDelete()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        string? payload = await repository.DequeueAsync();

        // Assert
        Assert.Null(payload);
        Assert.Equal(1, queue.GetMessageCallCount);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueAsync_WhenRetrievalFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed dequeue retrieval failure");
        var queue = new FakeCloudQueue
        {
            GetMessageException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            repository.DequeueAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(1, queue.GetMessageCallCount);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueAsync_WithMessage_ReturnsPayloadAndDeletesIt()
    {
        // Arrange
        const string payload = "single Δ payload";
        CloudQueueMessage message = CreateRetrievedMessage(
            payload,
            "single-message-id",
            "single-pop-receipt");
        var queue = new FakeCloudQueue
        {
            MessageToReturn = message
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        string? actual = await repository.DequeueAsync();

        // Assert
        Assert.Equal(payload, actual);
        Assert.Equal("single-message-id", message.Id);
        Assert.Equal("single-pop-receipt", message.PopReceipt);
        Assert.Same(message, Assert.Single(queue.DeleteByMessageCalls));
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueAsync_WhenDeleteFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed dequeue delete failure");
        CloudQueueMessage message = CreateRetrievedMessage(
            "payload",
            "delete-failure-id",
            "delete-failure-receipt");
        var queue = new FakeCloudQueue
        {
            MessageToReturn = message,
            DeleteByMessageException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            repository.DequeueAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Same(message, Assert.Single(queue.DeleteByMessageCalls));
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithEmptyQueue_ForwardsCountAndVisibility()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        IReadOnlyList<string> payloads = await repository.DequeueBatchAsync(1);

        // Assert
        Assert.Empty(payloads);
        GetMessagesCall call = Assert.Single(queue.GetMessagesCalls);
        Assert.Equal(1, call.Count);
        Assert.Equal(TimeSpan.FromMinutes(2), call.VisibilityTimeout);
        Assert.Null(call.Options);
        Assert.Null(call.Context);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithTwoMessages_PreservesAndDeletesInOrder()
    {
        // Arrange
        CloudQueueMessage first = CreateRetrievedMessage(
            "first résumé",
            "message-id-1",
            "pop-receipt-1");
        CloudQueueMessage second = CreateRetrievedMessage(
            "second 東京",
            "message-id-2",
            "pop-receipt-2");
        var queue = new FakeCloudQueue
        {
            MessagesToReturn = new[] { first, second }
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        IReadOnlyList<string> payloads = await repository.DequeueBatchAsync(8);

        // Assert
        Assert.Equal(new[] { "first résumé", "second 東京" }, payloads);
        GetMessagesCall retrieval = Assert.Single(queue.GetMessagesCalls);
        Assert.Equal(8, retrieval.Count);
        Assert.Equal(TimeSpan.FromMinutes(2), retrieval.VisibilityTimeout);
        Assert.Collection(
            queue.DeleteByIdentityCalls,
            deletion =>
            {
                Assert.Equal("message-id-1", deletion.Id);
                Assert.Equal("pop-receipt-1", deletion.PopReceipt);
            },
            deletion =>
            {
                Assert.Equal("message-id-2", deletion.Id);
                Assert.Equal("pop-receipt-2", deletion.PopReceipt);
            });
        Assert.Empty(queue.DeleteByMessageCalls);
    }

    [Fact]
    public async Task DequeueBatchAsync_WhenRetrievalFails_PropagatesAndDoesNotDelete()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed retrieval failure");
        var queue = new FakeCloudQueue
        {
            GetMessagesException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DequeueBatchAsync(3));

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(3, Assert.Single(queue.GetMessagesCalls).Count);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task DequeueBatchAsync_WhenDeleteFails_PropagatesAndStopsProcessing()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed batch delete failure");
        CloudQueueMessage first = CreateRetrievedMessage(
            "first",
            "delete-failure-id",
            "delete-failure-receipt");
        CloudQueueMessage second = CreateRetrievedMessage(
            "second",
            "unreached-id",
            "unreached-receipt");
        var queue = new FakeCloudQueue
        {
            MessagesToReturn = new[] { first, second },
            DeleteByIdentityException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DequeueBatchAsync(2));

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(
            ("delete-failure-id", "delete-failure-receipt"),
            Assert.Single(queue.DeleteByIdentityCalls));
        Assert.Empty(queue.DeleteByMessageCalls);
    }

    [Fact]
    public async Task RenewLeaseAsync_ForwardsMessageAndCallerVisibility()
    {
        // Arrange
        CloudQueueMessage message = CreateRetrievedMessage(
            "long-running payload",
            "lease-message-id",
            "lease-pop-receipt");
        TimeSpan extension = TimeSpan.FromSeconds(83);
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.RenewLeaseAsync(message, extension);

        // Assert
        UpdateMessageCall call = Assert.Single(queue.UpdateMessageCalls);
        Assert.Same(message, call.Message);
        Assert.Equal(extension, call.VisibilityTimeout);
        Assert.Equal(MessageUpdateFields.Visibility, call.UpdateFields);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task RenewLeaseAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed lease renewal failure");
        CloudQueueMessage message = CreateRetrievedMessage(
            "payload",
            "lease-failure-id",
            "lease-failure-receipt");
        var queue = new FakeCloudQueue
        {
            UpdateMessageException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RenewLeaseAsync(message, TimeSpan.FromSeconds(41)));

        // Assert
        Assert.Same(exception, actual);
        Assert.Same(message, Assert.Single(queue.UpdateMessageCalls).Message);
    }

    [Fact]
    public async Task PeekAsync_WithMessage_ReturnsPayloadWithoutDelete()
    {
        // Arrange
        CloudQueueMessage message = CreateRetrievedMessage(
            "peek résumé 東京",
            "peek-message-id",
            "peek-pop-receipt");
        var queue = new FakeCloudQueue
        {
            PeekMessageToReturn = message
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        string? payload = await repository.PeekAsync();

        // Assert
        Assert.Equal("peek résumé 東京", payload);
        Assert.Equal(1, queue.PeekMessageCallCount);
        Assert.Empty(queue.UpdateMessageCalls);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task PeekAsync_WhenQueueIsEmpty_ReturnsNull()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        string? payload = await repository.PeekAsync();

        // Assert
        Assert.Null(payload);
        Assert.Equal(1, queue.PeekMessageCallCount);
        Assert.Empty(queue.UpdateMessageCalls);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task PeekAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed peek failure");
        var queue = new FakeCloudQueue
        {
            PeekMessageException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            repository.PeekAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(1, queue.PeekMessageCallCount);
        Assert.Empty(queue.DeleteByMessageCalls);
        Assert.Empty(queue.DeleteByIdentityCalls);
    }

    [Fact]
    public async Task GetApproximateLengthAsync_WithNoServicePopulatedCount_FetchesAndReturnsDefault()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);
        Assert.Null(queue.ApproximateMessageCount);

        // Act
        int count = await repository.GetApproximateLengthAsync();

        // Assert
        Assert.Equal(0, count);
        Assert.Equal(1, queue.FetchAttributesCallCount);
        Assert.Null(queue.ApproximateMessageCount);
    }

    [Fact]
    public async Task GetApproximateLengthAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed attribute fetch failure");
        var queue = new FakeCloudQueue
        {
            FetchAttributesException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            repository.GetApproximateLengthAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(1, queue.FetchAttributesCallCount);
    }

    [Fact]
    public async Task ClearAsync_DelegatesOnce()
    {
        // Arrange
        var queue = new FakeCloudQueue();
        QueueRepository repository = CreateRepository(queue);

        // Act
        await repository.ClearAsync();

        // Assert
        Assert.Equal(1, queue.ClearCallCount);
    }

    [Fact]
    public async Task ClearAsync_WhenSdkFails_PropagatesSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("fixed clear failure");
        var queue = new FakeCloudQueue
        {
            ClearException = exception
        };
        QueueRepository repository = CreateRepository(queue);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            repository.ClearAsync);

        // Assert
        Assert.Same(exception, actual);
        Assert.Equal(1, queue.ClearCallCount);
    }

    private static QueueRepository CreateRepository(FakeCloudQueue queue)
    {
        return new QueueRepository(new FakeCloudQueueClient(queue), "unit-test-queue");
    }

    private static CloudQueueMessage CreateRetrievedMessage(
        string payload,
        string messageId,
        string popReceipt)
    {
        var message = new CloudQueueMessage(messageId, popReceipt);
        message.SetMessageContent(payload);
        return message;
    }

    private sealed class FakeCloudQueueClient : CloudQueueClient
    {
        private readonly FakeCloudQueue _queue;

        public FakeCloudQueueClient(FakeCloudQueue queue)
            : base(
                new Uri("https://contosodocs.queue.core.windows.net/"),
                new StorageCredentials())
        {
            _queue = queue;
        }

        public List<string> RequestedQueueNames { get; } = new();

        public override CloudQueue GetQueueReference(string queueName)
        {
            RequestedQueueNames.Add(queueName);
            return _queue;
        }
    }

    private sealed class FakeCloudQueue : CloudQueue
    {
        public FakeCloudQueue()
            : base(new Uri("https://contosodocs.queue.core.windows.net/unit-test-queue"))
        {
        }

        public int CreateIfNotExistsCallCount { get; private set; }

        public Exception? CreateIfNotExistsException { get; init; }

        public List<CloudQueueMessage> AddMessageCalls { get; } = new();

        public Exception? AddMessageException { get; init; }

        public List<AddMessageCall> AddMessageWithOptionsCalls { get; } = new();

        public Exception? AddMessageWithOptionsException { get; init; }

        public int GetMessageCallCount { get; private set; }

        public CloudQueueMessage? MessageToReturn { get; init; }

        public Exception? GetMessageException { get; init; }

        public List<GetMessagesCall> GetMessagesCalls { get; } = new();

        public IEnumerable<CloudQueueMessage> MessagesToReturn { get; init; } =
            Array.Empty<CloudQueueMessage>();

        public Exception? GetMessagesException { get; init; }

        public List<CloudQueueMessage> DeleteByMessageCalls { get; } = new();

        public Exception? DeleteByMessageException { get; init; }

        public List<(string Id, string PopReceipt)> DeleteByIdentityCalls { get; } = new();

        public Exception? DeleteByIdentityException { get; init; }

        public List<UpdateMessageCall> UpdateMessageCalls { get; } = new();

        public Exception? UpdateMessageException { get; init; }

        public int PeekMessageCallCount { get; private set; }

        public CloudQueueMessage? PeekMessageToReturn { get; init; }

        public Exception? PeekMessageException { get; init; }

        public int FetchAttributesCallCount { get; private set; }

        public Exception? FetchAttributesException { get; init; }

        public int ClearCallCount { get; private set; }

        public Exception? ClearException { get; init; }

        public override Task<bool> CreateIfNotExistsAsync()
        {
            CreateIfNotExistsCallCount++;

            return CreateIfNotExistsException is null
                ? Task.FromResult(true)
                : Task.FromException<bool>(CreateIfNotExistsException);
        }

        public override Task AddMessageAsync(CloudQueueMessage message)
        {
            AddMessageCalls.Add(message);
            return AddMessageException is null
                ? Task.CompletedTask
                : Task.FromException(AddMessageException);
        }

        public override Task AddMessageAsync(
            CloudQueueMessage message,
            TimeSpan? timeToLive,
            TimeSpan? initialVisibilityDelay,
            QueueRequestOptions options,
            OperationContext operationContext)
        {
            AddMessageWithOptionsCalls.Add(
                new AddMessageCall(
                    message,
                    timeToLive,
                    initialVisibilityDelay,
                    options,
                    operationContext));
            return AddMessageWithOptionsException is null
                ? Task.CompletedTask
                : Task.FromException(AddMessageWithOptionsException);
        }

        public override Task<CloudQueueMessage> GetMessageAsync()
        {
            GetMessageCallCount++;
            return GetMessageException is null
                ? Task.FromResult(MessageToReturn!)
                : Task.FromException<CloudQueueMessage>(GetMessageException);
        }

        public override Task<IEnumerable<CloudQueueMessage>> GetMessagesAsync(
            int messageCount,
            TimeSpan? visibilityTimeout,
            QueueRequestOptions options,
            OperationContext operationContext)
        {
            GetMessagesCalls.Add(
                new GetMessagesCall(
                    messageCount,
                    visibilityTimeout,
                    options,
                    operationContext));

            return GetMessagesException is null
                ? Task.FromResult(MessagesToReturn)
                : Task.FromException<IEnumerable<CloudQueueMessage>>(GetMessagesException);
        }

        public override Task DeleteMessageAsync(CloudQueueMessage message)
        {
            DeleteByMessageCalls.Add(message);
            return DeleteByMessageException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteByMessageException);
        }

        public override Task DeleteMessageAsync(string messageId, string popReceipt)
        {
            DeleteByIdentityCalls.Add((messageId, popReceipt));
            return DeleteByIdentityException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteByIdentityException);
        }

        public override Task UpdateMessageAsync(
            CloudQueueMessage message,
            TimeSpan visibilityTimeout,
            MessageUpdateFields updateFields)
        {
            UpdateMessageCalls.Add(new UpdateMessageCall(message, visibilityTimeout, updateFields));
            return UpdateMessageException is null
                ? Task.CompletedTask
                : Task.FromException(UpdateMessageException);
        }

        public override Task<CloudQueueMessage> PeekMessageAsync()
        {
            PeekMessageCallCount++;
            return PeekMessageException is null
                ? Task.FromResult(PeekMessageToReturn!)
                : Task.FromException<CloudQueueMessage>(PeekMessageException);
        }

        public override Task FetchAttributesAsync()
        {
            FetchAttributesCallCount++;
            return FetchAttributesException is null
                ? Task.CompletedTask
                : Task.FromException(FetchAttributesException);
        }

        public override Task ClearAsync()
        {
            ClearCallCount++;
            return ClearException is null
                ? Task.CompletedTask
                : Task.FromException(ClearException);
        }
    }

    private sealed record AddMessageCall(
        CloudQueueMessage Message,
        TimeSpan? TimeToLive,
        TimeSpan? InitialVisibilityDelay,
        QueueRequestOptions? Options,
        OperationContext? Context);

    private sealed record GetMessagesCall(
        int Count,
        TimeSpan? VisibilityTimeout,
        QueueRequestOptions? Options,
        OperationContext? Context);

    private sealed record UpdateMessageCall(
        CloudQueueMessage Message,
        TimeSpan VisibilityTimeout,
        MessageUpdateFields UpdateFields);
}

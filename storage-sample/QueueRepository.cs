using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Contoso.Documents
{
    public class QueueRepository
    {
        private readonly QueueClient _queue;

        public QueueRepository(QueueServiceClient client, string queueName)
            : this(client.GetQueueClient(queueName))
        {
        }

        public QueueRepository(QueueClient queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public async Task InitializeAsync() =>
            await _queue.CreateIfNotExistsAsync().ConfigureAwait(false);

        public async Task EnqueueAsync(string payload) =>
            await _queue.SendMessageAsync(payload ?? string.Empty).ConfigureAwait(false);

        public async Task EnqueueDelayedAsync(string payload, TimeSpan delay) =>
            await _queue.SendMessageAsync(payload ?? string.Empty, delay, TimeSpan.FromDays(7))
                .ConfigureAwait(false);

        public async Task<string> DequeueAsync()
        {
            QueueMessage message = (await _queue.ReceiveMessageAsync().ConfigureAwait(false)).Value;
            if (message is null)
            {
                return null;
            }

            string payload = message.MessageText;
            await _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
            return payload;
        }

        public async Task<IReadOnlyList<string>> DequeueBatchAsync(int count)
        {
            QueueMessage[] messages =
                (await _queue.ReceiveMessagesAsync(count, TimeSpan.FromMinutes(2)).ConfigureAwait(false)).Value;
            var payloads = new List<string>();

            foreach (QueueMessage message in messages)
            {
                payloads.Add(message.MessageText);
                await _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
            }

            return payloads;
        }

        public async Task RenewLeaseAsync(QueueMessage message, TimeSpan extension) =>
            await _queue
                .UpdateMessageAsync(message.MessageId, message.PopReceipt, message.MessageText, extension)
                .ConfigureAwait(false);

        public async Task<string> PeekAsync()
        {
            PeekedMessage message = (await _queue.PeekMessageAsync().ConfigureAwait(false)).Value;
            return message?.MessageText;
        }

        public async Task<int> GetApproximateLengthAsync()
        {
            QueueProperties properties = (await _queue.GetPropertiesAsync().ConfigureAwait(false)).Value;
            return properties.ApproximateMessagesCount;
        }

        public async Task ClearAsync() =>
            await _queue.ClearMessagesAsync().ConfigureAwait(false);
    }
}

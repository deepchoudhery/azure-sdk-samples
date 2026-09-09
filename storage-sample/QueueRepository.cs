using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Contoso.Documents
{
    /// <summary>
    /// Work queue for the document ingestion pipeline.
    /// </summary>
    public class QueueRepository
    {
        private readonly QueueClient _queue;

        public QueueRepository(QueueServiceClient client, string queueName)
        {
            _queue = client.GetQueueClient(queueName);
        }

        public async Task InitializeAsync()
        {
            await _queue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task EnqueueAsync(string payload)
        {
            await _queue.SendMessageAsync(payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Enqueues with a visibility delay and an explicit time to live.
        /// </summary>
        public async Task EnqueueDelayedAsync(string payload, TimeSpan delay)
        {
            await _queue
                .SendMessageAsync(
                    payload,
                    visibilityTimeout: delay,
                    timeToLive: TimeSpan.FromDays(7))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Classic single-message dequeue loop: get, process, delete using the message id and
        /// pop receipt held on the message object.
        /// </summary>
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
            var payloads = new List<string>();

            QueueMessage[] messages = (await _queue
                .ReceiveMessagesAsync(count, TimeSpan.FromMinutes(2))
                .ConfigureAwait(false)).Value;

            foreach (QueueMessage message in messages)
            {
                payloads.Add(message.MessageText);
                await _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);
            }

            return payloads;
        }

        /// <summary>
        /// Extends the invisibility window and returns the message with its refreshed pop receipt.
        /// </summary>
        public async Task<QueueMessage> RenewLeaseAsync(QueueMessage message, TimeSpan extension)
        {
            UpdateReceipt receipt = (await _queue
                    .UpdateMessageAsync(
                        message.MessageId,
                        message.PopReceipt,
                        messageText: null,
                        visibilityTimeout: extension)
                    .ConfigureAwait(false))
                .Value;

            return message.Update(receipt);
        }

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

        public async Task ClearAsync()
        {
            await _queue.ClearMessagesAsync().ConfigureAwait(false);
        }
    }
}

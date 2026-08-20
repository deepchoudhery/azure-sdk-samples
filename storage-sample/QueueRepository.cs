using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Contoso.Documents
{
    /// <summary>
    /// Work queue for the document ingestion pipeline.
    /// </summary>
    public class QueueRepository
    {
        private readonly CloudQueue _queue;

        public QueueRepository(CloudQueueClient client, string queueName)
        {
            _queue = client.GetQueueReference(queueName);
        }

        public async Task InitializeAsync()
        {
            await _queue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task EnqueueAsync(string payload)
        {
            var message = new CloudQueueMessage(payload);
            await _queue.AddMessageAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Enqueues with a visibility delay and an explicit time to live.
        /// </summary>
        public async Task EnqueueDelayedAsync(string payload, TimeSpan delay)
        {
            var message = new CloudQueueMessage(payload);

            await _queue
                .AddMessageAsync(message, TimeSpan.FromDays(7), delay, null, null)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Classic single-message dequeue loop: get, process, delete using the message id and
        /// pop receipt held on the message object.
        /// </summary>
        public async Task<string> DequeueAsync()
        {
            CloudQueueMessage message = await _queue.GetMessageAsync().ConfigureAwait(false);

            if (message is null)
            {
                return null;
            }

            string payload = message.AsString;

            await _queue.DeleteMessageAsync(message).ConfigureAwait(false);

            return payload;
        }

        public async Task<IReadOnlyList<string>> DequeueBatchAsync(int count)
        {
            var payloads = new List<string>();

            IEnumerable<CloudQueueMessage> messages = await _queue
                .GetMessagesAsync(count, TimeSpan.FromMinutes(2), null, null)
                .ConfigureAwait(false);

            foreach (CloudQueueMessage message in messages)
            {
                payloads.Add(message.AsString);
                await _queue.DeleteMessageAsync(message.Id, message.PopReceipt).ConfigureAwait(false);
            }

            return payloads;
        }

        /// <summary>
        /// Extends the invisibility window on a message that is taking longer than expected.
        /// </summary>
        public async Task RenewLeaseAsync(CloudQueueMessage message, TimeSpan extension)
        {
            await _queue
                .UpdateMessageAsync(message, extension, MessageUpdateFields.Visibility)
                .ConfigureAwait(false);
        }

        public async Task<string> PeekAsync()
        {
            CloudQueueMessage message = await _queue.PeekMessageAsync().ConfigureAwait(false);
            return message?.AsString;
        }

        public async Task<int> GetApproximateLengthAsync()
        {
            await _queue.FetchAttributesAsync().ConfigureAwait(false);
            return _queue.ApproximateMessageCount ?? 0;
        }

        public async Task ClearAsync()
        {
            await _queue.ClearAsync().ConfigureAwait(false);
        }
    }
}

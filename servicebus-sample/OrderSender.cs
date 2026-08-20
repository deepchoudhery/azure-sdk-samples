using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    /// <summary>
    /// Publishes orders onto the ordering queue. All of these send paths are synchronous or
    /// sync-over-async in the legacy SDK.
    /// </summary>
    public class OrderSender
    {
        private readonly QueueClient _queueClient;

        public OrderSender(QueueClient queueClient)
        {
            _queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
        }

        /// <summary>
        /// Fire-and-forget synchronous send.
        /// </summary>
        public void Send(OrderMessage order)
        {
            var message = new BrokeredMessage(order)
            {
                MessageId = order.OrderId,
                CorrelationId = order.CustomerId,
                Label = "order-placed",
                ContentType = "application/xml",
                TimeToLive = TimeSpan.FromHours(12),
            };

            message.Properties["region"] = order.Region;
            message.Properties["total"] = (double)order.Total;
            message.Properties["priority"] = order.Total > 1000m ? "high" : "normal";

            _queueClient.Send(message);
        }

        public async Task SendAsync(OrderMessage order)
        {
            var message = new BrokeredMessage(order)
            {
                MessageId = order.OrderId,
                Label = "order-placed",
            };

            message.Properties["region"] = order.Region;

            await _queueClient.SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Batched send. The legacy SDK silently fails if the batch exceeds the entity's max
        /// message size, so callers had to chunk by hand.
        /// </summary>
        public async Task SendBatchAsync(IEnumerable<OrderMessage> orders)
        {
            var batch = new List<BrokeredMessage>();

            foreach (OrderMessage order in orders)
            {
                var message = new BrokeredMessage(order)
                {
                    MessageId = order.OrderId,
                    Label = "order-placed",
                };

                message.Properties["region"] = order.Region;
                batch.Add(message);

                if (batch.Count == 100)
                {
                    await _queueClient.SendBatchAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _queueClient.SendBatchAsync(batch).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Schedules an order for later delivery. Note <see cref="DateTime"/> rather than
        /// <see cref="DateTimeOffset"/>.
        /// </summary>
        public async Task ScheduleAsync(OrderMessage order, DateTime enqueueAtUtc)
        {
            var message = new BrokeredMessage(order)
            {
                MessageId = order.OrderId,
                ScheduledEnqueueTimeUtc = enqueueAtUtc,
                Label = "order-scheduled",
            };

            message.Properties["region"] = order.Region;

            await _queueClient.SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends every message in a session so an ordered consumer sees them in sequence.
        /// </summary>
        public async Task SendSessionAsync(string sessionId, IEnumerable<OrderMessage> orders)
        {
            foreach (OrderMessage order in orders)
            {
                var message = new BrokeredMessage(order)
                {
                    SessionId = sessionId,
                    MessageId = order.OrderId,
                };

                await _queueClient.SendAsync(message).ConfigureAwait(false);
            }
        }

        public void Close()
        {
            _queueClient.Close();
        }
    }
}

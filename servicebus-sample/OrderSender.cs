using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Publishes orders onto the ordering queue.
    /// </summary>
    public class OrderSender
    {
        private readonly ServiceBusSender _sender;

        public OrderSender(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        /// <summary>
        /// Sends an order with the complete routing and priority metadata.
        /// </summary>
        public async Task SendWithMetadataAsync(OrderMessage order)
        {
            ServiceBusMessage message = CreateOrderMessage(order, "order-placed");
            message.CorrelationId = order.CustomerId;
            message.TimeToLive = TimeSpan.FromHours(12d);
            message.ApplicationProperties["total"] = (double)order.Total;
            message.ApplicationProperties["priority"] = order.Total > 1000m ? "high" : "normal";

            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }

        public async Task SendAsync(OrderMessage order)
        {
            ServiceBusMessage message = CreateOrderMessage(order, "order-placed");
            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Batches messages according to the entity's negotiated maximum message size.
        /// </summary>
        public async Task SendBatchAsync(IEnumerable<OrderMessage> orders)
        {
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }

            ServiceBusMessageBatch batch = await _sender.CreateMessageBatchAsync().ConfigureAwait(false);

            try
            {
                foreach (OrderMessage order in orders)
                {
                    ServiceBusMessage message = CreateOrderMessage(order, "order-placed");

                    if (batch.TryAddMessage(message))
                    {
                        continue;
                    }

                    if (batch.Count == 0)
                    {
                        throw CreateMessageTooLargeException(order);
                    }

                    await _sender.SendMessagesAsync(batch).ConfigureAwait(false);
                    batch.Dispose();
                    batch = await _sender.CreateMessageBatchAsync().ConfigureAwait(false);

                    if (!batch.TryAddMessage(message))
                    {
                        throw CreateMessageTooLargeException(order);
                    }
                }

                if (batch.Count > 0)
                {
                    await _sender.SendMessagesAsync(batch).ConfigureAwait(false);
                }
            }
            finally
            {
                batch.Dispose();
            }
        }

        /// <summary>
        /// Schedules an order for later delivery. Note <see cref="DateTime"/> rather than
        /// <see cref="DateTimeOffset"/>.
        /// </summary>
        public async Task ScheduleAsync(OrderMessage order, DateTime enqueueAtUtc)
        {
            ServiceBusMessage message = CreateOrderMessage(order, "order-scheduled");
            var scheduledEnqueueTime =
                new DateTimeOffset(DateTime.SpecifyKind(enqueueAtUtc, DateTimeKind.Utc));

            await _sender
                .ScheduleMessageAsync(message, scheduledEnqueueTime)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends every message in a session so an ordered consumer sees them in sequence.
        /// </summary>
        public async Task SendSessionAsync(string sessionId, IEnumerable<OrderMessage> orders)
        {
            foreach (OrderMessage order in orders)
            {
                ServiceBusMessage message = CreateOrderMessage(order, null);
                message.SessionId = sessionId;

                await _sender.SendMessageAsync(message).ConfigureAwait(false);
            }
        }

        private static ServiceBusMessage CreateOrderMessage(OrderMessage order, string subject)
        {
            ServiceBusMessage message = OrderMessageContract.CreateMessage(order);
            message.MessageId = order.OrderId;
            message.Subject = subject;
            message.ApplicationProperties["region"] = order.Region;
            return message;
        }

        private static InvalidOperationException CreateMessageTooLargeException(OrderMessage order)
        {
            return new InvalidOperationException(
                $"Order message '{order.OrderId}' exceeds the Service Bus entity's maximum message size.");
        }
    }
}

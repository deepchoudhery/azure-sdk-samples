using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Publishes JSON-encoded orders onto the ordering queue.
    /// </summary>
    public sealed class OrderSender : IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public OrderSender(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public async Task SendAsync(OrderMessage order)
        {
            ServiceBusMessage message = CreateMessage(order, "order-placed");
            message.CorrelationId = order.CustomerId;
            message.ContentType = "application/json";
            message.TimeToLive = TimeSpan.FromHours(12d);
            message.ApplicationProperties["total"] = (double)order.Total;
            message.ApplicationProperties["priority"] =
                order.Total > 1000m ? "high" : "normal";

            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }

        public async Task SendBatchAsync(IEnumerable<OrderMessage> orders)
        {
            ServiceBusMessageBatch batch = await _sender
                .CreateMessageBatchAsync()
                .ConfigureAwait(false);

            try
            {
                foreach (OrderMessage order in orders)
                {
                    ServiceBusMessage message = CreateMessage(order, "order-placed");
                    if (!batch.TryAddMessage(message))
                    {
                        if (batch.Count == 0)
                        {
                            throw new InvalidOperationException(
                                $"Order {order.OrderId} exceeds the Service Bus message size limit.");
                        }

                        await _sender.SendMessagesAsync(batch).ConfigureAwait(false);
                        batch.Dispose();
                        batch = await _sender.CreateMessageBatchAsync().ConfigureAwait(false);

                        if (!batch.TryAddMessage(message))
                        {
                            throw new InvalidOperationException(
                                $"Order {order.OrderId} exceeds the Service Bus message size limit.");
                        }
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

        public async Task ScheduleAsync(OrderMessage order, DateTimeOffset enqueueAt)
        {
            ServiceBusMessage message = CreateMessage(order, "order-scheduled");
            message.ScheduledEnqueueTime = enqueueAt.ToUniversalTime();

            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }

        public async Task SendSessionAsync(string sessionId, IEnumerable<OrderMessage> orders)
        {
            foreach (OrderMessage order in orders)
            {
                ServiceBusMessage message = CreateMessage(order, "order-placed");
                message.SessionId = sessionId;

                await _sender.SendMessageAsync(message).ConfigureAwait(false);
            }
        }

        public ValueTask DisposeAsync()
        {
            return _sender.DisposeAsync();
        }

        private static ServiceBusMessage CreateMessage(OrderMessage order, string subject)
        {
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(order))
            {
                MessageId = order.OrderId,
                Subject = subject,
                ContentType = "application/json",
            };
            message.ApplicationProperties["region"] = order.Region;
            return message;
        }
    }
}

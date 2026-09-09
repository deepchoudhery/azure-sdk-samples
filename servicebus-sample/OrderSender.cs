using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Publishes orders onto the ordering queue. All of these send paths are synchronous or
    /// sync-over-async in the legacy SDK.
    /// </summary>
    public sealed class OrderSender : IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public OrderSender(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        /// <summary>
        /// Sends one order.
        /// </summary>
        public async Task Send(
            OrderMessage order,
            CancellationToken cancellationToken = default)
        {
            ServiceBusMessage message = CreateMessage(order, "order-placed");
            message.CorrelationId = order.CustomerId;
            message.ContentType = "application/xml";
            message.TimeToLive = TimeSpan.FromHours(12);
            message.ApplicationProperties["total"] = (double)order.Total;
            message.ApplicationProperties["priority"] = order.Total > 1000m ? "high" : "normal";

            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _sender.SendMessageAsync(message, timeout.Token).ConfigureAwait(false);
        }

        public async Task SendAsync(
            OrderMessage order,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _sender
                .SendMessageAsync(CreateMessage(order, "order-placed"), timeout.Token)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends size-aware batches accepted by Service Bus.
        /// </summary>
        public async Task SendBatchAsync(
            IEnumerable<OrderMessage> orders,
            CancellationToken cancellationToken = default)
        {
            if (orders == null)
            {
                throw new ArgumentNullException(nameof(orders));
            }

            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            CancellationToken operationToken = timeout.Token;

            ServiceBusMessageBatch batch = await _sender
                .CreateMessageBatchAsync(operationToken)
                .ConfigureAwait(false);
            try
            {
                foreach (OrderMessage order in orders)
                {
                    operationToken.ThrowIfCancellationRequested();
                    ServiceBusMessage message = CreateMessage(order, "order-placed");
                    if (batch.TryAddMessage(message))
                    {
                        continue;
                    }

                    if (batch.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Order {order.OrderId} is too large for a Service Bus batch.");
                    }

                    await _sender.SendMessagesAsync(batch, operationToken).ConfigureAwait(false);
                    batch.Dispose();
                    batch = await _sender
                        .CreateMessageBatchAsync(operationToken)
                        .ConfigureAwait(false);

                    if (!batch.TryAddMessage(message))
                    {
                        throw new InvalidOperationException(
                            $"Order {order.OrderId} is too large for a Service Bus batch.");
                    }
                }

                if (batch.Count > 0)
                {
                    await _sender.SendMessagesAsync(batch, operationToken).ConfigureAwait(false);
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
        public async Task ScheduleAsync(
            OrderMessage order,
            DateTime enqueueAtUtc,
            CancellationToken cancellationToken = default)
        {
            DateTime utc = enqueueAtUtc.Kind == DateTimeKind.Utc
                ? enqueueAtUtc
                : DateTime.SpecifyKind(enqueueAtUtc, DateTimeKind.Utc);

            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _sender
                .ScheduleMessageAsync(
                    CreateMessage(order, "order-scheduled"),
                    new DateTimeOffset(utc),
                    timeout.Token)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends every message in a session so an ordered consumer sees them in sequence.
        /// </summary>
        public async Task SendSessionAsync(
            string sessionId,
            IEnumerable<OrderMessage> orders,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);

            foreach (OrderMessage order in orders)
            {
                timeout.Token.ThrowIfCancellationRequested();
                ServiceBusMessage message = CreateMessage(order, null);
                message.SessionId = sessionId;

                await _sender.SendMessageAsync(message, timeout.Token).ConfigureAwait(false);
            }
        }

        public ValueTask DisposeAsync()
        {
            return _sender.DisposeAsync();
        }

        private static ServiceBusMessage CreateMessage(OrderMessage order, string subject)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var message = new ServiceBusMessage(OrderMessage.Serialize(order))
            {
                MessageId = order.OrderId,
                Subject = subject,
            };

            message.ApplicationProperties["region"] = order.Region;
            return message;
        }
    }
}

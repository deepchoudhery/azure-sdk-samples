using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    /// <summary>
    /// Consumes the ordering queue with the legacy <c>OnMessage</c> pump and settles each
    /// message by hand.
    /// </summary>
    public class OrderProcessor
    {
        private readonly QueueClient _queueClient;
        private readonly Action<OrderMessage> _handler;

        public OrderProcessor(QueueClient queueClient, Action<OrderMessage> handler)
        {
            _queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Synchronous message pump with manual settlement.
        /// </summary>
        public void Start()
        {
            var options = new OnMessageOptions
            {
                AutoComplete = false,
                MaxConcurrentCalls = 4,
                AutoRenewTimeout = TimeSpan.FromMinutes(1),
            };

            options.ExceptionReceived += OnExceptionReceived;

            _queueClient.OnMessage(
                message =>
                {
                    try
                    {
                        OrderMessage order = message.GetBody<OrderMessage>();

                        object region;
                        if (message.Properties.TryGetValue("region", out region))
                        {
                            Console.WriteLine($"Handling {order.OrderId} for region {region}.");
                        }

                        _handler(order);
                        message.Complete();
                    }
                    catch (SerializationException)
                    {
                        // Poison payload — never going to succeed, so remove it from the queue.
                        message.DeadLetter("DeserializationFailed", "Body was not an OrderMessage.");
                    }
                    catch (Exception)
                    {
                        if (message.DeliveryCount >= 5)
                        {
                            message.DeadLetter("TooManyAttempts", "Exceeded retry budget.");
                        }
                        else
                        {
                            message.Abandon();
                        }
                    }
                },
                options);
        }

        /// <summary>
        /// Async variant of the same pump.
        /// </summary>
        public void StartAsync()
        {
            var options = new OnMessageOptions
            {
                AutoComplete = false,
                MaxConcurrentCalls = 8,
            };

            options.ExceptionReceived += OnExceptionReceived;

            _queueClient.OnMessageAsync(
                async message =>
                {
                    try
                    {
                        OrderMessage order = message.GetBody<OrderMessage>();
                        _handler(order);
                        await message.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        await message.AbandonAsync().ConfigureAwait(false);
                    }
                },
                options);
        }

        /// <summary>
        /// Explicit pull-based drain, used by the nightly reconciliation job.
        /// </summary>
        public async Task<IReadOnlyList<OrderMessage>> DrainAsync(int maxMessages)
        {
            var drained = new List<OrderMessage>();

            IEnumerable<BrokeredMessage> batch = await _queueClient
                .ReceiveBatchAsync(maxMessages, TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);

            foreach (BrokeredMessage message in batch)
            {
                drained.Add(message.GetBody<OrderMessage>());
                await message.CompleteAsync().ConfigureAwait(false);
            }

            return drained;
        }

        /// <summary>
        /// Peeks a single message without removing it from the queue.
        /// </summary>
        public async Task<OrderMessage> PeekAsync()
        {
            BrokeredMessage message = await _queueClient.PeekAsync().ConfigureAwait(false);
            return message?.GetBody<OrderMessage>();
        }

        public void Stop()
        {
            _queueClient.Close();
        }

        private static void OnExceptionReceived(object sender, ExceptionReceivedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.Error.WriteLine($"Service Bus error during {e.Action}: {e.Exception.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Consumes the ordering queue with manual settlement.
    /// </summary>
    public sealed class OrderProcessor : IAsyncDisposable
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly string _queueName;
        private readonly Action<OrderMessage> _handler;
        private ServiceBusProcessor _processor;
        private ServiceBusReceiver _receiver;

        public OrderProcessor(
            ServiceBusClient serviceBusClient,
            string queueName,
            Action<OrderMessage> handler)
        {
            _serviceBusClient =
                serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
            _queueName = string.IsNullOrWhiteSpace(queueName)
                ? throw new ArgumentException("A queue name is required.", nameof(queueName))
                : queueName;
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Starts the standard message pump with four concurrent callbacks and one minute of
        /// automatic lock renewal.
        /// </summary>
        public Task StartAsync()
        {
            return StartProcessorAsync(
                maxConcurrentCalls: 4,
                maxAutoLockRenewalDuration: TimeSpan.FromMinutes(1d),
                ProcessMessageAsync);
        }

        /// <summary>
        /// Starts the alternate high-concurrency pump with the legacy default five-minute
        /// automatic lock-renewal window.
        /// </summary>
        public Task StartHighConcurrencyAsync()
        {
            return StartProcessorAsync(
                maxConcurrentCalls: 8,
                maxAutoLockRenewalDuration: TimeSpan.FromMinutes(5d),
                ProcessMessageHighConcurrencyAsync);
        }

        /// <summary>
        /// Explicit pull-based drain, used by the nightly reconciliation job.
        /// </summary>
        public async Task<IReadOnlyList<OrderMessage>> DrainAsync(int maxMessages)
        {
            ServiceBusReceiver receiver = GetReceiver();
            IReadOnlyList<ServiceBusReceivedMessage> batch = await receiver
                .ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5d))
                .ConfigureAwait(false);

            var drained = new List<OrderMessage>(batch.Count);

            foreach (ServiceBusReceivedMessage message in batch)
            {
                drained.Add(OrderMessageContract.Read(message));
                await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
            }

            return drained;
        }

        /// <summary>
        /// Peeks a single message without removing it from the queue.
        /// </summary>
        public async Task<OrderMessage> PeekAsync()
        {
            ServiceBusReceivedMessage message = await GetReceiver()
                .PeekMessageAsync()
                .ConfigureAwait(false);

            return message == null ? null : OrderMessageContract.Read(message);
        }

        public async Task StopAsync()
        {
            if (_processor != null)
            {
                if (_processor.IsProcessing)
                {
                    await _processor.StopProcessingAsync().ConfigureAwait(false);
                }

                await _processor.DisposeAsync().ConfigureAwait(false);
                _processor = null;
            }

            if (_receiver != null)
            {
                await _receiver.DisposeAsync().ConfigureAwait(false);
                _receiver = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }

        private async Task StartProcessorAsync(
            int maxConcurrentCalls,
            TimeSpan maxAutoLockRenewalDuration,
            Func<ProcessMessageEventArgs, Task> messageHandler)
        {
            if (_processor != null)
            {
                throw new InvalidOperationException("The order processor has already been started.");
            }

            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = maxConcurrentCalls,
                MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            };

            ServiceBusProcessor processor = _serviceBusClient.CreateProcessor(_queueName, options);
            processor.ProcessMessageAsync += messageHandler;
            processor.ProcessErrorAsync += ProcessErrorAsync;
            _processor = processor;

            try
            {
                await processor.StartProcessingAsync().ConfigureAwait(false);
            }
            catch
            {
                _processor = null;
                await processor.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            try
            {
                OrderMessage order = OrderMessageContract.Read(args.Message);

                object region;
                if (args.Message.ApplicationProperties.TryGetValue("region", out region))
                {
                    Console.WriteLine($"Handling {order.OrderId} for region {region}.");
                }

                _handler(order);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSerializationException(ex))
            {
                await args
                    .DeadLetterMessageAsync(
                        args.Message,
                        "DeserializationFailed",
                        "Body was not an OrderMessage.")
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (args.Message.DeliveryCount >= 5)
                {
                    await args
                        .DeadLetterMessageAsync(
                            args.Message,
                            "TooManyAttempts",
                            "Exceeded retry budget.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
                }
            }
        }

        private async Task ProcessMessageHighConcurrencyAsync(ProcessMessageEventArgs args)
        {
            try
            {
                OrderMessage order = OrderMessageContract.Read(args.Message);
                _handler(order);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await args.AbandonMessageAsync(args.Message).ConfigureAwait(false);
            }
        }

        private static bool IsSerializationException(Exception exception)
        {
            return exception is JsonException || exception is SerializationException;
        }

        private static Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.Error.WriteLine(
                $"Service Bus error during {args.ErrorSource} on {args.EntityPath}: " +
                args.Exception.Message);
            return Task.CompletedTask;
        }

        private ServiceBusReceiver GetReceiver()
        {
            if (_receiver == null)
            {
                _receiver = _serviceBusClient.CreateReceiver(
                    _queueName,
                    new ServiceBusReceiverOptions
                    {
                        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    });
            }

            return _receiver;
        }
    }
}

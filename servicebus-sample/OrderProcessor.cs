using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Consumes the ordering queue with a peek-lock processor and settles each message by hand.
    /// </summary>
    public sealed class OrderProcessor : IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusReceiver _receiver;
        private readonly string _queueName;
        private readonly Action<OrderMessage> _handler;
        private ServiceBusProcessor _processor;
        private bool _processorStarted;

        public OrderProcessor(
            ServiceBusClient client,
            string queueName,
            Action<OrderMessage> handler)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _queueName = string.IsNullOrWhiteSpace(queueName)
                ? throw new ArgumentException("A queue name is required.", nameof(queueName))
                : queueName;
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _receiver = _client.CreateReceiver(
                _queueName,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
        }

        /// <summary>
        /// Synchronous message pump with manual settlement.
        /// </summary>
        public Task Start(CancellationToken cancellationToken = default)
        {
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(1),
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            };

            return StartProcessorAsync(
                options,
                async args =>
                {
                    using CancellationTokenSource timeout =
                        ServiceBusOperationPolicy.CreateCancellationSource(args.CancellationToken);

                    try
                    {
                        OrderMessage order = OrderMessage.Deserialize(args.Message.Body);

                        if (args.Message.ApplicationProperties.TryGetValue("region", out object region))
                        {
                            Console.WriteLine($"Handling {order.OrderId} for region {region}.");
                        }

                        _handler(order);
                        await args
                            .CompleteMessageAsync(args.Message, timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (SerializationException)
                    {
                        // Poison payload — never going to succeed, so remove it from the queue.
                        await args
                            .DeadLetterMessageAsync(
                                args.Message,
                                "DeserializationFailed",
                                "Body was not an OrderMessage.",
                                timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        if (args.Message.DeliveryCount >= 5)
                        {
                            await args
                                .DeadLetterMessageAsync(
                                    args.Message,
                                    "TooManyAttempts",
                                    "Exceeded retry budget.",
                                    timeout.Token)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await args
                                .AbandonMessageAsync(args.Message, cancellationToken: timeout.Token)
                                .ConfigureAwait(false);
                        }
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Async variant of the same pump.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 8,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            };

            return StartProcessorAsync(
                options,
                async args =>
                {
                    using CancellationTokenSource timeout =
                        ServiceBusOperationPolicy.CreateCancellationSource(args.CancellationToken);

                    try
                    {
                        OrderMessage order = OrderMessage.Deserialize(args.Message.Body);
                        _handler(order);
                        await args
                            .CompleteMessageAsync(args.Message, timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        await args
                            .AbandonMessageAsync(args.Message, cancellationToken: timeout.Token)
                            .ConfigureAwait(false);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Explicit pull-based drain, used by the nightly reconciliation job.
        /// </summary>
        public async Task<IReadOnlyList<OrderMessage>> DrainAsync(
            int maxMessages,
            CancellationToken cancellationToken = default)
        {
            var drained = new List<OrderMessage>();
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);

            IReadOnlyList<ServiceBusReceivedMessage> batch = await _receiver
                .ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), timeout.Token)
                .ConfigureAwait(false);

            foreach (ServiceBusReceivedMessage message in batch)
            {
                timeout.Token.ThrowIfCancellationRequested();
                drained.Add(OrderMessage.Deserialize(message.Body));
                await _receiver
                    .CompleteMessageAsync(message, timeout.Token)
                    .ConfigureAwait(false);
            }

            return drained;
        }

        /// <summary>
        /// Peeks a single message without removing it from the queue.
        /// </summary>
        public async Task<OrderMessage> PeekAsync(
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            ServiceBusReceivedMessage message = await _receiver
                .PeekMessageAsync(cancellationToken: timeout.Token)
                .ConfigureAwait(false);
            return message == null ? null : OrderMessage.Deserialize(message.Body);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_processor != null && _processorStarted)
            {
                using CancellationTokenSource timeout =
                    ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
                await _processor.StopProcessingAsync(timeout.Token).ConfigureAwait(false);
                _processorStarted = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);

            if (_processor != null)
            {
                await _processor.DisposeAsync().ConfigureAwait(false);
            }

            await _receiver.DisposeAsync().ConfigureAwait(false);
        }

        private async Task StartProcessorAsync(
            ServiceBusProcessorOptions options,
            Func<ProcessMessageEventArgs, Task> handler,
            CancellationToken cancellationToken)
        {
            if (_processor != null)
            {
                throw new InvalidOperationException("The order processor has already been configured.");
            }

            _processor = _client.CreateProcessor(_queueName, options);
            _processor.ProcessMessageAsync += handler;
            _processor.ProcessErrorAsync += OnProcessErrorAsync;
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _processor.StartProcessingAsync(timeout.Token).ConfigureAwait(false);
            _processorStarted = true;
        }

        private static Task OnProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.Error.WriteLine(
                $"Service Bus error during {args.ErrorSource}: {args.Exception.Message}");
            return Task.CompletedTask;
        }
    }
}

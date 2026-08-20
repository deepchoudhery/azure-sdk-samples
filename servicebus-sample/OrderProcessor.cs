using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    public sealed class OrderProcessor : IAsyncDisposable
    {
        private readonly ServiceBusClientProvider _provider;
        private readonly ServiceBusReceiver _receiver;
        private readonly Action<OrderMessage> _handler;
        private ServiceBusProcessor _processor;

        public OrderProcessor(
            ServiceBusClientProvider provider,
            ServiceBusReceiver receiver,
            Action<OrderMessage> handler)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task Start()
        {
            return StartProcessorAsync(4, TimeSpan.FromMinutes(1d));
        }

        public Task StartAsync()
        {
            return StartProcessorAsync(8, TimeSpan.FromMinutes(5d));
        }

        public async Task<IReadOnlyList<OrderMessage>> DrainAsync(int maxMessages)
        {
            var drained = new List<OrderMessage>();

            IReadOnlyList<ServiceBusReceivedMessage> batch = await _receiver
                .ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5d))
                .ConfigureAwait(false);

            foreach (ServiceBusReceivedMessage message in batch)
            {
                drained.Add(message.Body.ToObjectFromJson<OrderMessage>());
                await _receiver.CompleteMessageAsync(message).ConfigureAwait(false);
            }

            return drained;
        }

        public async Task<OrderMessage> PeekAsync()
        {
            ServiceBusReceivedMessage message = await _receiver
                .PeekMessageAsync()
                .ConfigureAwait(false);
            return message?.Body.ToObjectFromJson<OrderMessage>();
        }

        public async Task StopAsync()
        {
            if (_processor != null && _processor.IsProcessing)
            {
                await _processor.StopProcessingAsync().ConfigureAwait(false);
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
            int maxConcurrentCalls,
            TimeSpan maxAutoLockRenewalDuration)
        {
            if (_processor != null)
            {
                throw new InvalidOperationException("The order processor has already been started.");
            }

            _processor = _provider.CreateQueueProcessor(
                TopologyManager.OrderQueuePath,
                maxConcurrentCalls,
                maxAutoLockRenewalDuration);
            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;
            await _processor.StartProcessingAsync().ConfigureAwait(false);
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            try
            {
                OrderMessage order = args.Message.Body.ToObjectFromJson<OrderMessage>();

                if (args.Message.ApplicationProperties.TryGetValue("region", out object region))
                {
                    Console.WriteLine($"Handling {order.OrderId} for region {region}.");
                }

                _handler(order);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
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

        private static Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.Error.WriteLine(
                $"Service Bus error during {args.ErrorSource}: {args.Exception.Message}");
            return Task.CompletedTask;
        }
    }
}

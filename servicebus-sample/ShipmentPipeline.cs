using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Fan-out side of the pipeline: shipments are published to a topic and each downstream
    /// team owns a filtered subscription.
    /// </summary>
    public sealed class ShipmentPublisher : IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public ShipmentPublisher(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public async Task PublishAsync(
            OrderMessage order,
            string carrier,
            CancellationToken cancellationToken = default)
        {
            var message = new ServiceBusMessage(OrderMessage.Serialize(order))
            {
                MessageId = $"{order.OrderId}:{carrier}",
                Subject = "shipment-ready",
            };

            message.ApplicationProperties["region"] = order.Region;
            message.ApplicationProperties["carrier"] = carrier;
            message.ApplicationProperties["expedited"] = order.Total > 500m;

            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _sender.SendMessageAsync(message, timeout.Token).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            return _sender.DisposeAsync();
        }
    }

    /// <summary>
    /// Consumes one subscription of the shipments topic.
    /// </summary>
    public sealed class ShipmentSubscriber : IAsyncDisposable
    {
        private readonly ServiceBusProcessor _processor;
        private bool _processorStarted;

        public ShipmentSubscriber(
            ServiceBusClient client,
            string topicName,
            string subscriptionName)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _processor = client.CreateProcessor(
                topicName,
                subscriptionName,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 2,
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                });
        }

        public async Task StartAsync(
            Func<OrderMessage, string, Task> onShipment,
            CancellationToken cancellationToken = default)
        {
            if (onShipment == null)
            {
                throw new ArgumentNullException(nameof(onShipment));
            }

            _processor.ProcessMessageAsync +=
                async message =>
                {
                    using CancellationTokenSource timeout =
                        ServiceBusOperationPolicy.CreateCancellationSource(
                            message.CancellationToken);
                    OrderMessage order = OrderMessage.Deserialize(message.Message.Body);

                    message.Message.ApplicationProperties.TryGetValue("carrier", out object carrier);

                    try
                    {
                        await onShipment(order, carrier as string).ConfigureAwait(false);
                        await message
                            .CompleteMessageAsync(message.Message, timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await message
                            .DeadLetterMessageAsync(
                                message.Message,
                                "ShipmentHandlerFailed",
                                ex.Message,
                                timeout.Token)
                            .ConfigureAwait(false);
                    }
                };

            _processor.ProcessErrorAsync += args =>
            {
                Console.Error.WriteLine($"Subscription error: {args.Exception.Message}");
                return Task.CompletedTask;
            };

            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await _processor.StartProcessingAsync(timeout.Token).ConfigureAwait(false);
            _processorStarted = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_processorStarted)
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
            await _processor.DisposeAsync().ConfigureAwait(false);
        }
    }
}

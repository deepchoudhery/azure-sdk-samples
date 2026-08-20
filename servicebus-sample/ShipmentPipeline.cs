using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    public sealed class ShipmentPublisher : IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public ShipmentPublisher(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public async Task PublishAsync(OrderMessage order, string carrier)
        {
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(order))
            {
                MessageId = $"{order.OrderId}:{carrier}",
                Subject = "shipment-ready",
                ContentType = "application/json",
            };

            message.ApplicationProperties["region"] = order.Region;
            message.ApplicationProperties["carrier"] = carrier;
            message.ApplicationProperties["expedited"] = order.Total > 500m;

            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            return _sender.DisposeAsync();
        }
    }

    public sealed class ShipmentSubscriber : IAsyncDisposable
    {
        private readonly ServiceBusClientProvider _provider;
        private ServiceBusProcessor _processor;
        private Func<OrderMessage, string, Task> _onShipment;

        public ShipmentSubscriber(ServiceBusClientProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task StartAsync(Func<OrderMessage, string, Task> onShipment)
        {
            if (_processor != null)
            {
                throw new InvalidOperationException("The shipment subscriber has already been started.");
            }

            _onShipment = onShipment ?? throw new ArgumentNullException(nameof(onShipment));
            _processor = _provider.CreateSubscriptionProcessor(
                TopologyManager.ShipmentTopicPath,
                "expedited",
                2);
            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;
            await _processor.StartProcessingAsync().ConfigureAwait(false);
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
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            OrderMessage order = args.Message.Body.ToObjectFromJson<OrderMessage>();
            args.Message.ApplicationProperties.TryGetValue("carrier", out object carrier);

            try
            {
                await _onShipment(order, carrier as string).ConfigureAwait(false);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await args
                    .DeadLetterMessageAsync(
                        args.Message,
                        "ShipmentHandlerFailed",
                        ex.Message)
                    .ConfigureAwait(false);
            }
        }

        private static Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.Error.WriteLine($"Subscription error: {args.Exception.Message}");
            return Task.CompletedTask;
        }
    }
}

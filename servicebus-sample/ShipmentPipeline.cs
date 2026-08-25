using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Fan-out side of the pipeline: shipments are published to a topic and each downstream
    /// team owns a filtered subscription.
    /// </summary>
    public class ShipmentPublisher
    {
        private readonly ServiceBusSender _sender;

        public ShipmentPublisher(ServiceBusSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public async Task PublishAsync(OrderMessage order, string carrier)
        {
            ServiceBusMessage message = OrderMessageContract.CreateMessage(order);
            message.MessageId = $"{order.OrderId}:{carrier}";
            message.Subject = "shipment-ready";
            message.ApplicationProperties["region"] = order.Region;
            message.ApplicationProperties["carrier"] = carrier;
            message.ApplicationProperties["expedited"] = order.Total > 500m;

            await _sender.SendMessageAsync(message).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Consumes one subscription of the shipments topic.
    /// </summary>
    public sealed class ShipmentSubscriber : IAsyncDisposable
    {
        private readonly ServiceBusProcessor _processor;
        private Func<OrderMessage, string, Task> _onShipment;

        public ShipmentSubscriber(
            ServiceBusClient serviceBusClient,
            string topicName,
            string subscriptionName)
        {
            if (serviceBusClient == null)
            {
                throw new ArgumentNullException(nameof(serviceBusClient));
            }

            if (string.IsNullOrWhiteSpace(topicName))
            {
                throw new ArgumentException("A topic name is required.", nameof(topicName));
            }

            if (string.IsNullOrWhiteSpace(subscriptionName))
            {
                throw new ArgumentException(
                    "A subscription name is required.",
                    nameof(subscriptionName));
            }

            _processor = serviceBusClient.CreateProcessor(
                topicName,
                subscriptionName,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = 2,
                    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5d),
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                });

            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;
        }

        public async Task StartAsync(Func<OrderMessage, string, Task> onShipment)
        {
            _onShipment = onShipment ?? throw new ArgumentNullException(nameof(onShipment));
            await _processor.StartProcessingAsync().ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            if (_processor.IsProcessing)
            {
                await _processor.StopProcessingAsync().ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            await _processor.DisposeAsync().ConfigureAwait(false);
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            OrderMessage order = OrderMessageContract.Read(args.Message);

            object carrier;
            args.Message.ApplicationProperties.TryGetValue("carrier", out carrier);

            try
            {
                await _onShipment(order, carrier as string).ConfigureAwait(false);
                await args.CompleteMessageAsync(args.Message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await args
                    .DeadLetterMessageAsync(args.Message, "ShipmentHandlerFailed", ex.Message)
                    .ConfigureAwait(false);
            }
        }

        private static Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.Error.WriteLine(
                $"Subscription error during {args.ErrorSource} on {args.EntityPath}: " +
                args.Exception.Message);
            return Task.CompletedTask;
        }
    }
}

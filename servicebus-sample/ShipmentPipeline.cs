using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.ServiceBus.Messaging;

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
    public class ShipmentSubscriber
    {
        private readonly SubscriptionClient _subscriptionClient;

        public ShipmentSubscriber(SubscriptionClient subscriptionClient)
        {
            _subscriptionClient = subscriptionClient
                ?? throw new ArgumentNullException(nameof(subscriptionClient));
        }

        public void Start(Func<OrderMessage, string, Task> onShipment)
        {
            var options = new OnMessageOptions
            {
                AutoComplete = false,
                MaxConcurrentCalls = 2,
            };

            options.ExceptionReceived += (sender, e) =>
            {
                if (e.Exception != null)
                {
                    Console.Error.WriteLine($"Subscription error: {e.Exception.Message}");
                }
            };

            _subscriptionClient.OnMessageAsync(
                async message =>
                {
                    OrderMessage order = message.GetBody<OrderMessage>();

                    object carrier;
                    message.Properties.TryGetValue("carrier", out carrier);

                    try
                    {
                        await onShipment(order, carrier as string).ConfigureAwait(false);
                        await message.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await message
                            .DeadLetterAsync("ShipmentHandlerFailed", ex.Message)
                            .ConfigureAwait(false);
                    }
                },
                options);
        }

        public void Stop()
        {
            _subscriptionClient.Close();
        }
    }
}

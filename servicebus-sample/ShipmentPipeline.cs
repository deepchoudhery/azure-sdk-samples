using System;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    /// <summary>
    /// Fan-out side of the pipeline: shipments are published to a topic and each downstream
    /// team owns a filtered subscription.
    /// </summary>
    public class ShipmentPublisher
    {
        private readonly TopicClient _topicClient;

        public ShipmentPublisher(TopicClient topicClient)
        {
            _topicClient = topicClient ?? throw new ArgumentNullException(nameof(topicClient));
        }

        public async Task PublishAsync(OrderMessage order, string carrier)
        {
            var message = new BrokeredMessage(order)
            {
                MessageId = $"{order.OrderId}:{carrier}",
                Label = "shipment-ready",
            };

            message.Properties["region"] = order.Region;
            message.Properties["carrier"] = carrier;
            message.Properties["expedited"] = order.Total > 500m;

            await _topicClient.SendAsync(message).ConfigureAwait(false);
        }

        public void Close()
        {
            _topicClient.Close();
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

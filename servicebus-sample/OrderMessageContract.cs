using System;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Defines the JSON wire format used for modern Service Bus order messages.
    /// </summary>
    public static class OrderMessageContract
    {
        public const string ContentType = "application/json";

        public static ServiceBusMessage CreateMessage(OrderMessage order)
        {
            return new ServiceBusMessage(Serialize(order))
            {
                ContentType = ContentType,
            };
        }

        public static OrderMessage Read(ServiceBusReceivedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Deserialize(message.Body);
        }

        public static BinaryData Serialize(OrderMessage order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            return BinaryData.FromObjectAsJson(order);
        }

        public static OrderMessage Deserialize(BinaryData body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            return body.ToObjectFromJson<OrderMessage>();
        }
    }
}

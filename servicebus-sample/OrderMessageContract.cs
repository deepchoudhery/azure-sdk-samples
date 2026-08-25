using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Defines the JSON wire format used for modern Service Bus order messages.
    /// </summary>
    public static class OrderMessageContract
    {
        private const int MaxLegacyBodyBytes = 1024 * 1024;

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

            try
            {
                OrderMessage order = body.ToObjectFromJson<OrderMessage>();
                return order ?? throw new SerializationException(
                    "The JSON message body did not contain an OrderMessage.");
            }
            catch (JsonException)
            {
                return DeserializeLegacy(body);
            }
        }

        private static OrderMessage DeserializeLegacy(BinaryData body)
        {
            if (body.ToMemory().Length > MaxLegacyBodyBytes)
            {
                throw new SerializationException(
                    $"Legacy order message bodies cannot exceed {MaxLegacyBodyBytes} bytes.");
            }

            var quotas = new XmlDictionaryReaderQuotas
            {
                MaxArrayLength = 16,
                MaxBytesPerRead = 4096,
                MaxDepth = 16,
                MaxNameTableCharCount = 4096,
                MaxStringContentLength = MaxLegacyBodyBytes,
            };
            var serializer = new DataContractSerializer(
                typeof(OrderMessage),
                new DataContractSerializerSettings
                {
                    MaxItemsInObjectGraph = 16,
                });

            try
            {
                using Stream stream = body.ToStream();
                using XmlDictionaryReader reader =
                    XmlDictionaryReader.CreateBinaryReader(stream, quotas);
                object value = serializer.ReadObject(reader, verifyObjectName: true);

                if (value is not OrderMessage order)
                {
                    throw new SerializationException(
                        "The legacy message body did not contain an OrderMessage.");
                }

                if (reader.MoveToContent() != XmlNodeType.None)
                {
                    throw new SerializationException(
                        "The legacy OrderMessage body contains trailing data.");
                }

                return order;
            }
            catch (XmlException ex)
            {
                throw new SerializationException(
                    "The legacy order message body is not valid binary XML.",
                    ex);
            }
        }
    }
}

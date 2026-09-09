using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Contoso.Ordering
{
    /// <summary>
    /// Body of every message on the ordering queue. The binary XML
    /// <see cref="DataContractSerializer"/> representation is retained for wire compatibility.
    /// </summary>
    [DataContract(Name = "Order", Namespace = "http://contoso.com/ordering")]
    public class OrderMessage
    {
        private const int MaximumBodySize = 1024 * 1024;
        private const int MaximumItemsInObjectGraph = 65536;

        [DataMember(Order = 1)]
        public string OrderId { get; set; }

        [DataMember(Order = 2)]
        public string CustomerId { get; set; }

        [DataMember(Order = 3)]
        public string Region { get; set; }

        [DataMember(Order = 4)]
        public decimal Total { get; set; }

        [DataMember(Order = 5)]
        public DateTime PlacedOnUtc { get; set; }

        public override string ToString()
        {
            return $"{OrderId} ({CustomerId}, {Region}) = {Total:C}";
        }

        public static BinaryData Serialize(OrderMessage order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var serializer = CreateSerializer();
            using var stream = new MemoryStream();
            byte[] payload;
            using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
            {
                serializer.WriteObject(writer, order);
                writer.Flush();
                payload = stream.ToArray();
            }

            if (payload.Length > MaximumBodySize)
            {
                throw new SerializationException(
                    $"The serialized order exceeds the {MaximumBodySize}-byte body limit.");
            }

            return new BinaryData(payload);
        }

        public static OrderMessage Deserialize(BinaryData body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            ReadOnlyMemory<byte> payload = body.ToMemory();
            if (payload.Length > MaximumBodySize)
            {
                throw new SerializationException(
                    $"The order body exceeds the {MaximumBodySize}-byte body limit.");
            }

            var quotas = new XmlDictionaryReaderQuotas
            {
                MaxArrayLength = MaximumBodySize,
                MaxBytesPerRead = 4096,
                MaxDepth = 64,
                MaxNameTableCharCount = 16384,
                MaxStringContentLength = MaximumBodySize,
            };

            using XmlDictionaryReader reader =
                XmlDictionaryReader.CreateBinaryReader(payload.ToArray(), quotas);

            return (OrderMessage)CreateSerializer().ReadObject(reader);
        }

        private static DataContractSerializer CreateSerializer()
        {
            return new DataContractSerializer(
                typeof(OrderMessage),
                new DataContractSerializerSettings
                {
                    MaxItemsInObjectGraph = MaximumItemsInObjectGraph,
                });
        }
    }
}

using System;
using System.Runtime.Serialization;

namespace Contoso.Ordering
{
    /// <summary>
    /// Body of every message on the ordering queue. Messages use the shared JSON contract in
    /// <see cref="OrderMessageContract"/>; the data contract annotations preserve the established
    /// serialized contract.
    /// </summary>
    [DataContract(Name = "Order", Namespace = "http://contoso.com/ordering")]
    public class OrderMessage
    {
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
    }
}

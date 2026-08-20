using System;
using Azure;
using Azure.Data.Tables;

namespace Contoso.Documents
{
    public class DocumentEntity : ITableEntity
    {
        public DocumentEntity()
        {
        }

        public DocumentEntity(string customerId, string documentId)
        {
            PartitionKey = customerId;
            RowKey = documentId;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string BlobName { get; set; }
        public string ContentType { get; set; }
        public long SizeInBytes { get; set; }
        public string Status { get; set; }
        public DateTime IndexedOnUtc { get; set; }
        public bool IsArchived { get; set; }
        public string CustomerId => PartitionKey;
        public string DocumentId => RowKey;
    }
}

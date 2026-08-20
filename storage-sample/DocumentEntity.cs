using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Contoso.Documents
{
    /// <summary>
    /// Table row describing one stored document. Inherits <see cref="TableEntity"/> to pick up
    /// PartitionKey/RowKey/Timestamp/ETag plus reflection-based (de)serialization.
    /// </summary>
    public class DocumentEntity : TableEntity
    {
        public DocumentEntity()
        {
        }

        public DocumentEntity(string customerId, string documentId)
        {
            PartitionKey = customerId;
            RowKey = documentId;
        }

        public string BlobName { get; set; }

        public string ContentType { get; set; }

        public long SizeInBytes { get; set; }

        public string Status { get; set; }

        public DateTime IndexedOnUtc { get; set; }

        public bool IsArchived { get; set; }

        public string CustomerId
        {
            get { return PartitionKey; }
        }

        public string DocumentId
        {
            get { return RowKey; }
        }
    }
}

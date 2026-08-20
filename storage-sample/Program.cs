using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Contoso.Documents
{
    public static class Program
    {
        private const string ConnectionStringVariable = "CONTOSO_STORAGE_CONNECTION";

        private const string DocumentsContainer = "documents";
        private const string IngestionQueue = "ingestion";
        private const string DocumentsTable = "documentindex";
        private const string PartnerShare = "partner-drop";

        public static async Task<int> Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine(
                    $"Set {ConnectionStringVariable} to run against a real storage account.");
                Console.WriteLine("Nothing to do — exiting without contacting Azure.");
                return 0;
            }

            StorageAccountFactory factory;
            if (!StorageAccountFactory.TryCreate(connectionString, out factory))
            {
                Console.Error.WriteLine($"{ConnectionStringVariable} is not a valid connection string.");
                return 1;
            }

            var blobs = new BlobRepository(factory.CreateBlobClient(), DocumentsContainer);
            var queue = new QueueRepository(factory.CreateQueueClient(), IngestionQueue);
            var table = new TableRepository(factory.CreateTableClient(), DocumentsTable);
            var files = new FileShareRepository(factory.CreateFileClient(), PartnerShare);

            await blobs.InitializeAsync().ConfigureAwait(false);
            await queue.InitializeAsync().ConfigureAwait(false);
            await table.InitializeAsync().ConfigureAwait(false);
            await files.InitializeAsync().ConfigureAwait(false);

            await IngestAsync(blobs, queue, table).ConfigureAwait(false);
            await ReportAsync(blobs, queue, table, files).ConfigureAwait(false);

            return 0;
        }

        private static async Task IngestAsync(
            BlobRepository blobs,
            QueueRepository queue,
            TableRepository table)
        {
            const string customerId = "CUST-001";
            var documentId = Guid.NewGuid().ToString("N");
            var blobName = $"{customerId}/{documentId}.txt";

            var payload = Encoding.UTF8.GetBytes("Invoice 12345 — total 420.00");

            using (var stream = new MemoryStream(payload))
            {
                await blobs.UploadAsync(blobName, stream, "text/plain").ConfigureAwait(false);
            }

            await table.UpsertAsync(new DocumentEntity(customerId, documentId)
            {
                BlobName = blobName,
                ContentType = "text/plain",
                SizeInBytes = payload.Length,
                Status = "Uploaded",
                IndexedOnUtc = DateTime.UtcNow,
                IsArchived = false,
            }).ConfigureAwait(false);

            await queue.EnqueueAsync($"{customerId}|{documentId}").ConfigureAwait(false);

            await blobs.AppendAuditLineAsync(customerId, $"uploaded {documentId}")
                .ConfigureAwait(false);
        }

        private static async Task ReportAsync(
            BlobRepository blobs,
            QueueRepository queue,
            TableRepository table,
            FileShareRepository files)
        {
            IReadOnlyList<string> blobNames = await blobs.ListAsync("CUST-001/").ConfigureAwait(false);
            Console.WriteLine($"Blobs for CUST-001: {blobNames.Count}");

            IReadOnlyList<DocumentEntity> documents =
                await table.ListForCustomerAsync("CUST-001").ConfigureAwait(false);
            Console.WriteLine($"Indexed documents: {documents.Count}");

            var pending = await queue.GetApproximateLengthAsync().ConfigureAwait(false);
            Console.WriteLine($"Pending ingestion messages: {pending}");

            IReadOnlyList<string> partnerFiles = await files.ListAsync("inbound").ConfigureAwait(false);
            Console.WriteLine($"Partner drop files: {partnerFiles.Count}");
        }
    }
}

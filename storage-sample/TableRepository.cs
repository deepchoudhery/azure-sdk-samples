using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Contoso.Documents
{
    /// <summary>
    /// Document metadata index.
    /// </summary>
    public class TableRepository
    {
        private readonly TableClient _table;

        public TableRepository(TableServiceClient client, string tableName)
        {
            _table = client.GetTableClient(tableName);
        }

        public async Task InitializeAsync()
        {
            await _table.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task InsertAsync(DocumentEntity document)
        {
            await _table.AddEntityAsync(document).ConfigureAwait(false);
        }

        public async Task UpsertAsync(DocumentEntity document)
        {
            await _table.UpsertEntityAsync(document, TableUpdateMode.Replace).ConfigureAwait(false);
        }

        public async Task MergeAsync(DocumentEntity document)
        {
            await _table.UpsertEntityAsync(document, TableUpdateMode.Merge).ConfigureAwait(false);
        }

        public async Task<DocumentEntity> GetAsync(string customerId, string documentId)
        {
            NullableResponse<DocumentEntity> result = await _table
                .GetEntityIfExistsAsync<DocumentEntity>(customerId, documentId)
                .ConfigureAwait(false);
            return result.HasValue ? result.Value : null;
        }

        public async Task DeleteAsync(string customerId, string documentId)
        {
            DocumentEntity existing = await GetAsync(customerId, documentId).ConfigureAwait(false);

            if (existing is null)
            {
                return;
            }

            await _table
                .DeleteEntityAsync(existing.PartitionKey, existing.RowKey, existing.ETag)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Paged query over one partition for active documents.
        /// </summary>
        public async Task<IReadOnlyList<DocumentEntity>> ListForCustomerAsync(string customerId)
        {
            var results = new List<DocumentEntity>();
            await foreach (DocumentEntity document in _table.QueryAsync<DocumentEntity>(
                document => document.PartitionKey == customerId && !document.IsArchived,
                maxPerPage: 200))
            {
                results.Add(document);
            }

            return results;
        }

        /// <summary>
        /// Schema-less read used by the admin tool, which does not know the entity shape.
        /// </summary>
        public async Task<IReadOnlyList<TableEntity>> DumpPartitionAsync(
            string customerId)
        {
            var rows = new List<TableEntity>();
            await foreach (TableEntity entity in _table.QueryAsync<TableEntity>(
                entity => entity.PartitionKey == customerId))
            {
                rows.Add(entity);
            }

            return rows;
        }

        /// <summary>
        /// Batch writes. The legacy SDK caps a batch at 100 entities within a single partition.
        /// </summary>
        public async Task UpsertBatchAsync(IEnumerable<DocumentEntity> documents)
        {
            var batch = new List<TableTransactionAction>(100);

            foreach (DocumentEntity document in documents)
            {
                batch.Add(new TableTransactionAction(
                    TableTransactionActionType.UpsertReplace,
                    document));

                if (batch.Count == 100)
                {
                    await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
                    batch = new List<TableTransactionAction>(100);
                }
            }

            if (batch.Count > 0)
            {
                await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Optimistic concurrency: replace only if the stored ETag still matches.
        /// </summary>
        public async Task<bool> TryReplaceAsync(DocumentEntity document)
        {
            try
            {
                await _table
                    .UpdateEntityAsync(document, document.ETag, TableUpdateMode.Replace)
                    .ConfigureAwait(false);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return false;
            }
        }
    }
}

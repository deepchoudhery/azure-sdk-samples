using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Contoso.Documents
{
    /// <summary>
    /// Document metadata index backed by Azure Tables.
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
        /// Segmented query over one partition, filtered with the string-built filter DSL.
        /// </summary>
        public async Task<IReadOnlyList<DocumentEntity>> ListForCustomerAsync(string customerId)
        {
            var results = new List<DocumentEntity>();
            string filter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {customerId} and IsArchived eq {false}");

            await foreach (DocumentEntity entity in _table
                .QueryAsync<DocumentEntity>(filter, maxPerPage: 200)
                .ConfigureAwait(false))
            {
                results.Add(entity);
                if (results.Count == 200)
                {
                    break;
                }
            }

            return results;
        }

        /// <summary>
        /// Schema-less read used by the admin tool, which does not know the entity shape.
        /// </summary>
        public async Task<IReadOnlyList<TableEntity>> DumpPartitionAsync(
            string customerId)
        {
            string filter = TableClient.CreateQueryFilter($"PartitionKey eq {customerId}");
            var rows = new List<TableEntity>();

            await foreach (TableEntity entity in _table
                .QueryAsync<TableEntity>(filter)
                .ConfigureAwait(false))
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
            var batchesByPartition =
                new Dictionary<string, List<TableTransactionAction>>(StringComparer.Ordinal);

            foreach (DocumentEntity document in documents)
            {
                if (!batchesByPartition.TryGetValue(document.PartitionKey, out var batch))
                {
                    batch = new List<TableTransactionAction>(100);
                    batchesByPartition.Add(document.PartitionKey, batch);
                }

                batch.Add(new TableTransactionAction(
                    TableTransactionActionType.UpsertReplace,
                    document));

                if (batch.Count == 100)
                {
                    await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
                    batchesByPartition[document.PartitionKey] =
                        new List<TableTransactionAction>(100);
                }
            }

            foreach (List<TableTransactionAction> batch in batchesByPartition.Values)
            {
                if (batch.Count > 0)
                {
                    await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
                }
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

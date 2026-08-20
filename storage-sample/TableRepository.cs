using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Contoso.Documents
{
    /// <summary>
    /// Document metadata index. Every write goes through a <see cref="TableOperation"/> that is
    /// then handed to <c>ExecuteAsync</c>.
    /// </summary>
    public class TableRepository
    {
        private readonly CloudTable _table;

        public TableRepository(CloudTableClient client, string tableName)
        {
            _table = client.GetTableReference(tableName);
        }

        public async Task InitializeAsync()
        {
            await _table.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task InsertAsync(DocumentEntity document)
        {
            TableOperation operation = TableOperation.Insert(document);
            await _table.ExecuteAsync(operation).ConfigureAwait(false);
        }

        public async Task UpsertAsync(DocumentEntity document)
        {
            TableOperation operation = TableOperation.InsertOrReplace(document);
            await _table.ExecuteAsync(operation).ConfigureAwait(false);
        }

        public async Task MergeAsync(DocumentEntity document)
        {
            TableOperation operation = TableOperation.InsertOrMerge(document);
            await _table.ExecuteAsync(operation).ConfigureAwait(false);
        }

        public async Task<DocumentEntity> GetAsync(string customerId, string documentId)
        {
            TableOperation operation =
                TableOperation.Retrieve<DocumentEntity>(customerId, documentId);

            TableResult result = await _table.ExecuteAsync(operation).ConfigureAwait(false);

            return result.Result as DocumentEntity;
        }

        public async Task DeleteAsync(string customerId, string documentId)
        {
            DocumentEntity existing = await GetAsync(customerId, documentId).ConfigureAwait(false);

            if (existing is null)
            {
                return;
            }

            TableOperation operation = TableOperation.Delete(existing);
            await _table.ExecuteAsync(operation).ConfigureAwait(false);
        }

        /// <summary>
        /// Segmented query over one partition, filtered with the string-built filter DSL.
        /// </summary>
        public async Task<IReadOnlyList<DocumentEntity>> ListForCustomerAsync(string customerId)
        {
            string partitionFilter = TableQuery.GenerateFilterCondition(
                "PartitionKey",
                QueryComparisons.Equal,
                customerId);

            string activeFilter = TableQuery.GenerateFilterConditionForBool(
                "IsArchived",
                QueryComparisons.Equal,
                false);

            var query = new TableQuery<DocumentEntity>()
                .Where(TableQuery.CombineFilters(partitionFilter, TableOperators.And, activeFilter))
                .Take(200);

            var results = new List<DocumentEntity>();
            TableContinuationToken token = null;

            do
            {
                TableQuerySegment<DocumentEntity> segment = await _table
                    .ExecuteQuerySegmentedAsync(query, token)
                    .ConfigureAwait(false);

                results.AddRange(segment.Results);
                token = segment.ContinuationToken;
            }
            while (token != null);

            return results;
        }

        /// <summary>
        /// Schema-less read used by the admin tool, which does not know the entity shape.
        /// </summary>
        public async Task<IReadOnlyList<IDictionary<string, EntityProperty>>> DumpPartitionAsync(
            string customerId)
        {
            string filter = TableQuery.GenerateFilterCondition(
                "PartitionKey",
                QueryComparisons.Equal,
                customerId);

            var query = new TableQuery<DynamicTableEntity>().Where(filter);

            var rows = new List<IDictionary<string, EntityProperty>>();
            TableContinuationToken token = null;

            do
            {
                TableQuerySegment<DynamicTableEntity> segment = await _table
                    .ExecuteQuerySegmentedAsync(query, token)
                    .ConfigureAwait(false);

                foreach (DynamicTableEntity entity in segment.Results)
                {
                    rows.Add(entity.Properties);
                }

                token = segment.ContinuationToken;
            }
            while (token != null);

            return rows;
        }

        /// <summary>
        /// Batch writes. The legacy SDK caps a batch at 100 entities within a single partition.
        /// </summary>
        public async Task UpsertBatchAsync(IEnumerable<DocumentEntity> documents)
        {
            var batch = new TableBatchOperation();

            foreach (DocumentEntity document in documents)
            {
                batch.InsertOrReplace(document);

                if (batch.Count == 100)
                {
                    await _table.ExecuteBatchAsync(batch).ConfigureAwait(false);
                    batch = new TableBatchOperation();
                }
            }

            if (batch.Count > 0)
            {
                await _table.ExecuteBatchAsync(batch).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Optimistic concurrency: replace only if the stored ETag still matches.
        /// </summary>
        public async Task<bool> TryReplaceAsync(DocumentEntity document)
        {
            try
            {
                TableOperation operation = TableOperation.Replace(document);
                await _table.ExecuteAsync(operation).ConfigureAwait(false);
                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 412)
            {
                return false;
            }
        }
    }
}

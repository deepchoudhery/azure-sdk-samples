using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Contoso.Documents
{
    public class TableRepository
    {
        private readonly TableClient _table;

        public TableRepository(TableServiceClient client, string tableName)
        {
            _table = client.GetTableClient(tableName);
        }

        public async Task InitializeAsync() =>
            await _table.CreateIfNotExistsAsync().ConfigureAwait(false);

        public async Task InsertAsync(DocumentEntity document) =>
            await _table.AddEntityAsync(document).ConfigureAwait(false);

        public async Task UpsertAsync(DocumentEntity document) =>
            await _table.UpsertEntityAsync(document, TableUpdateMode.Replace).ConfigureAwait(false);

        public async Task MergeAsync(DocumentEntity document) =>
            await _table.UpsertEntityAsync(document, TableUpdateMode.Merge).ConfigureAwait(false);

        public async Task<DocumentEntity> GetAsync(string customerId, string documentId)
        {
            NullableResponse<DocumentEntity> response =
                await _table.GetEntityIfExistsAsync<DocumentEntity>(customerId, documentId)
                    .ConfigureAwait(false);
            return response.HasValue ? response.Value : null;
        }

        public async Task DeleteAsync(string customerId, string documentId)
        {
            DocumentEntity existing = await GetAsync(customerId, documentId).ConfigureAwait(false);
            if (existing is not null)
            {
                await _table.DeleteEntityAsync(customerId, documentId, existing.ETag)
                    .ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<DocumentEntity>> ListForCustomerAsync(string customerId)
        {
            string filter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {customerId} and IsArchived eq {false}");
            return await CollectAtMostAsync(
                    _table.QueryAsync<DocumentEntity>(filter, 200),
                    200)
                .ConfigureAwait(false);
        }

        internal static async Task<IReadOnlyList<DocumentEntity>> CollectAtMostAsync(
            AsyncPageable<DocumentEntity> entities,
            int limit)
        {
            var results = new List<DocumentEntity>();
            await foreach (DocumentEntity entity in entities)
            {
                results.Add(entity);
                if (results.Count == limit)
                {
                    break;
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<IDictionary<string, object>>> DumpPartitionAsync(
            string customerId)
        {
            string filter = TableClient.CreateQueryFilter($"PartitionKey eq {customerId}");
            var rows = new List<IDictionary<string, object>>();
            await foreach (TableEntity entity in _table.QueryAsync<TableEntity>(filter))
            {
                rows.Add(GetCustomProperties(entity));
            }
            return rows;
        }

        internal static IDictionary<string, object> GetCustomProperties(
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            var customProperties = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in properties)
            {
                if (property.Key != nameof(ITableEntity.PartitionKey)
                    && property.Key != nameof(ITableEntity.RowKey)
                    && property.Key != nameof(ITableEntity.Timestamp)
                    && property.Key != nameof(ITableEntity.ETag))
                {
                    customProperties.Add(property.Key, property.Value);
                }
            }

            return customProperties;
        }

        public async Task UpsertBatchAsync(IEnumerable<DocumentEntity> documents)
        {
            var batch = new List<TableTransactionAction>(100);
            foreach (DocumentEntity document in documents)
            {
                batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, document));
                if (batch.Count == 100)
                {
                    await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _table.SubmitTransactionAsync(batch).ConfigureAwait(false);
            }
        }

        public async Task<bool> TryReplaceAsync(DocumentEntity document)
        {
            try
            {
                await _table.UpdateEntityAsync(document, document.ETag, TableUpdateMode.Replace)
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

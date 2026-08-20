using System;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    /// <summary>
    /// Creates the queue, topic, and subscriptions at startup. <see cref="NamespaceManager"/>
    /// is the legacy management surface and lives in the same package as the data plane.
    /// </summary>
    public class TopologyManager
    {
        public const string OrderQueuePath = "orders";
        public const string ShipmentTopicPath = "shipments";

        private readonly NamespaceManager _namespaceManager;

        public TopologyManager(string connectionString)
        {
            _namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
        }

        public async Task EnsureTopologyAsync()
        {
            await EnsureOrderQueueAsync().ConfigureAwait(false);
            await EnsureShipmentTopicAsync().ConfigureAwait(false);
            await EnsureSubscriptionsAsync().ConfigureAwait(false);
        }

        private async Task EnsureOrderQueueAsync()
        {
            if (await _namespaceManager.QueueExistsAsync(OrderQueuePath).ConfigureAwait(false))
            {
                return;
            }

            var description = new QueueDescription(OrderQueuePath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                LockDuration = TimeSpan.FromMinutes(1),
                MaxDeliveryCount = 5,
                EnableDeadLetteringOnMessageExpiration = true,
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                EnablePartitioning = false,
            };

            await _namespaceManager.CreateQueueAsync(description).ConfigureAwait(false);
        }

        private async Task EnsureShipmentTopicAsync()
        {
            if (await _namespaceManager.TopicExistsAsync(ShipmentTopicPath).ConfigureAwait(false))
            {
                return;
            }

            var description = new TopicDescription(ShipmentTopicPath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                EnableBatchedOperations = true,
            };

            await _namespaceManager.CreateTopicAsync(description).ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionsAsync()
        {
            await EnsureSubscriptionAsync("expedited", "expedited = true").ConfigureAwait(false);
            await EnsureSubscriptionAsync("west", "region = 'west'").ConfigureAwait(false);
            await EnsureSubscriptionAsync("audit", null).ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionAsync(string name, string sqlFilter)
        {
            bool exists = await _namespaceManager
                .SubscriptionExistsAsync(ShipmentTopicPath, name)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            var description = new SubscriptionDescription(ShipmentTopicPath, name)
            {
                LockDuration = TimeSpan.FromMinutes(1),
                MaxDeliveryCount = 5,
                EnableDeadLetteringOnMessageExpiration = true,
            };

            if (string.IsNullOrEmpty(sqlFilter))
            {
                await _namespaceManager.CreateSubscriptionAsync(description).ConfigureAwait(false);
            }
            else
            {
                await _namespaceManager
                    .CreateSubscriptionAsync(description, new SqlFilter(sqlFilter))
                    .ConfigureAwait(false);
            }
        }

        public async Task<long> GetQueueDepthAsync()
        {
            QueueDescription description = await _namespaceManager
                .GetQueueAsync(OrderQueuePath)
                .ConfigureAwait(false);

            return description.MessageCountDetails.ActiveMessageCount;
        }

        public async Task DeleteTopologyAsync()
        {
            if (await _namespaceManager.QueueExistsAsync(OrderQueuePath).ConfigureAwait(false))
            {
                await _namespaceManager.DeleteQueueAsync(OrderQueuePath).ConfigureAwait(false);
            }

            if (await _namespaceManager.TopicExistsAsync(ShipmentTopicPath).ConfigureAwait(false))
            {
                await _namespaceManager.DeleteTopicAsync(ShipmentTopicPath).ConfigureAwait(false);
            }
        }
    }
}

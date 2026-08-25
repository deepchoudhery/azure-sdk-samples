using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

namespace Contoso.Ordering
{
    /// <summary>
    /// Creates the queue, topic, and subscriptions at startup.
    /// </summary>
    public class TopologyManager
    {
        public const string OrderQueuePath = "orders";
        public const string ShipmentTopicPath = "shipments";

        private readonly ServiceBusAdministrationClient _administrationClient;

        public TopologyManager(string connectionString)
        {
            _administrationClient = new ServiceBusAdministrationClient(connectionString);
        }

        public async Task EnsureTopologyAsync()
        {
            await EnsureOrderQueueAsync().ConfigureAwait(false);
            await EnsureShipmentTopicAsync().ConfigureAwait(false);
            await EnsureSubscriptionsAsync().ConfigureAwait(false);
        }

        private async Task EnsureOrderQueueAsync()
        {
            if ((await _administrationClient.QueueExistsAsync(OrderQueuePath).ConfigureAwait(false)).Value)
            {
                return;
            }

            var options = new CreateQueueOptions(OrderQueuePath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7d),
                LockDuration = TimeSpan.FromMinutes(1d),
                MaxDeliveryCount = 5,
                DeadLetteringOnMessageExpiration = true,
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10d),
                EnablePartitioning = false,
            };

            await _administrationClient.CreateQueueAsync(options).ConfigureAwait(false);
        }

        private async Task EnsureShipmentTopicAsync()
        {
            if ((await _administrationClient.TopicExistsAsync(ShipmentTopicPath).ConfigureAwait(false)).Value)
            {
                return;
            }

            var options = new CreateTopicOptions(ShipmentTopicPath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7d),
                EnableBatchedOperations = true,
            };

            await _administrationClient.CreateTopicAsync(options).ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionsAsync()
        {
            await EnsureSubscriptionAsync("expedited", "expedited = true").ConfigureAwait(false);
            await EnsureSubscriptionAsync("west", "region = 'west'").ConfigureAwait(false);
            await EnsureSubscriptionAsync("audit", null).ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionAsync(string name, string sqlFilter)
        {
            bool exists = (await _administrationClient
                    .SubscriptionExistsAsync(ShipmentTopicPath, name)
                    .ConfigureAwait(false))
                .Value;

            if (exists)
            {
                return;
            }

            var options = new CreateSubscriptionOptions(ShipmentTopicPath, name)
            {
                LockDuration = TimeSpan.FromMinutes(1d),
                MaxDeliveryCount = 5,
                DeadLetteringOnMessageExpiration = true,
            };

            if (string.IsNullOrEmpty(sqlFilter))
            {
                await _administrationClient.CreateSubscriptionAsync(options).ConfigureAwait(false);
            }
            else
            {
                var rule = new CreateRuleOptions(
                    CreateRuleOptions.DefaultRuleName,
                    new SqlRuleFilter(sqlFilter));

                await _administrationClient
                    .CreateSubscriptionAsync(options, rule)
                    .ConfigureAwait(false);
            }
        }

        public async Task<long> GetQueueDepthAsync()
        {
            QueueRuntimeProperties properties = (await _administrationClient
                    .GetQueueRuntimePropertiesAsync(OrderQueuePath)
                    .ConfigureAwait(false))
                .Value;

            return properties.ActiveMessageCount;
        }

        public async Task DeleteTopologyAsync()
        {
            if ((await _administrationClient.QueueExistsAsync(OrderQueuePath).ConfigureAwait(false)).Value)
            {
                await _administrationClient.DeleteQueueAsync(OrderQueuePath).ConfigureAwait(false);
            }

            if ((await _administrationClient.TopicExistsAsync(ShipmentTopicPath).ConfigureAwait(false)).Value)
            {
                await _administrationClient.DeleteTopicAsync(ShipmentTopicPath).ConfigureAwait(false);
            }
        }
    }
}

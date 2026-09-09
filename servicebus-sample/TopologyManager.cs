using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

namespace Contoso.Ordering
{
    /// <summary>
    /// Creates the queue, topic, and subscriptions at startup using the Service Bus
    /// data-plane administration client.
    /// </summary>
    public class TopologyManager
    {
        public const string OrderQueuePath = "orders";
        public const string ShipmentTopicPath = "shipments";

        private readonly ServiceBusAdministrationClient _administrationClient;

        public TopologyManager(string connectionString)
        {
            _administrationClient = new ServiceBusAdministrationClient(
                connectionString,
                ServiceBusOperationPolicy.CreateAdministrationClientOptions());
        }

        public async Task EnsureTopologyAsync(
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            await EnsureOrderQueueAsync(timeout.Token).ConfigureAwait(false);
            await EnsureShipmentTopicAsync(timeout.Token).ConfigureAwait(false);
            await EnsureSubscriptionsAsync(timeout.Token).ConfigureAwait(false);
        }

        private async Task EnsureOrderQueueAsync(CancellationToken cancellationToken)
        {
            if ((await _administrationClient
                .QueueExistsAsync(OrderQueuePath, cancellationToken)
                .ConfigureAwait(false)).Value)
            {
                return;
            }

            var options = new CreateQueueOptions(OrderQueuePath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                LockDuration = TimeSpan.FromMinutes(1),
                MaxDeliveryCount = 5,
                DeadLetteringOnMessageExpiration = true,
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                EnablePartitioning = false,
            };

            await _administrationClient
                .CreateQueueAsync(options, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task EnsureShipmentTopicAsync(CancellationToken cancellationToken)
        {
            if ((await _administrationClient
                .TopicExistsAsync(ShipmentTopicPath, cancellationToken)
                .ConfigureAwait(false)).Value)
            {
                return;
            }

            var options = new CreateTopicOptions(ShipmentTopicPath)
            {
                MaxSizeInMegabytes = 5120,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                EnableBatchedOperations = true,
            };

            await _administrationClient
                .CreateTopicAsync(options, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionsAsync(CancellationToken cancellationToken)
        {
            await EnsureSubscriptionAsync(
                "expedited",
                "expedited = true",
                cancellationToken).ConfigureAwait(false);
            await EnsureSubscriptionAsync(
                "west",
                "region = 'west'",
                cancellationToken).ConfigureAwait(false);
            await EnsureSubscriptionAsync("audit", null, cancellationToken).ConfigureAwait(false);
        }

        private async Task EnsureSubscriptionAsync(
            string name,
            string sqlFilter,
            CancellationToken cancellationToken)
        {
            bool exists = (await _administrationClient
                .SubscriptionExistsAsync(ShipmentTopicPath, name, cancellationToken)
                .ConfigureAwait(false)).Value;

            if (exists)
            {
                return;
            }

            var options = new CreateSubscriptionOptions(ShipmentTopicPath, name)
            {
                LockDuration = TimeSpan.FromMinutes(1),
                MaxDeliveryCount = 5,
                DeadLetteringOnMessageExpiration = true,
            };

            if (string.IsNullOrEmpty(sqlFilter))
            {
                await _administrationClient
                    .CreateSubscriptionAsync(options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var rule = new CreateRuleOptions(
                    RuleProperties.DefaultRuleName,
                    new SqlRuleFilter(sqlFilter));

                await _administrationClient
                    .CreateSubscriptionAsync(options, rule, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<long> GetQueueDepthAsync(
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);
            QueueRuntimeProperties properties = (await _administrationClient
                .GetQueueRuntimePropertiesAsync(OrderQueuePath, timeout.Token)
                .ConfigureAwait(false)).Value;

            return properties.ActiveMessageCount;
        }

        public async Task DeleteTopologyAsync(
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout =
                ServiceBusOperationPolicy.CreateCancellationSource(cancellationToken);

            if ((await _administrationClient
                .QueueExistsAsync(OrderQueuePath, timeout.Token)
                .ConfigureAwait(false)).Value)
            {
                await _administrationClient
                    .DeleteQueueAsync(OrderQueuePath, timeout.Token)
                    .ConfigureAwait(false);
            }

            if ((await _administrationClient
                .TopicExistsAsync(ShipmentTopicPath, timeout.Token)
                .ConfigureAwait(false)).Value)
            {
                await _administrationClient
                    .DeleteTopicAsync(ShipmentTopicPath, timeout.Token)
                    .ConfigureAwait(false);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    public static class Program
    {
        private const string ConnectionStringVariable = "CONTOSO_SERVICEBUS_CONNECTION";

        public static int Main(string[] args)
        {
            return RunAsync().GetAwaiter().GetResult();
        }

        private static async Task<int> RunAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine(
                    $"Set {ConnectionStringVariable} to run against a real Service Bus namespace.");
                Console.WriteLine("Nothing to do — exiting without contacting Azure.");
                return 0;
            }

            var topology = new TopologyManager(connectionString);
            await topology.EnsureTopologyAsync().ConfigureAwait(false);

            using (MessagingFactoryProvider provider =
                MessagingFactoryProvider.FromConnectionString(connectionString))
            {
                QueueClient sendClient = provider.CreateQueueClient(TopologyManager.OrderQueuePath);
                QueueClient receiveClient = provider.CreateQueueClient(
                    TopologyManager.OrderQueuePath,
                    ReceiveMode.PeekLock);

                TopicClient topicClient =
                    provider.CreateTopicClient(TopologyManager.ShipmentTopicPath);

                SubscriptionClient subscriptionClient = provider.CreateSubscriptionClient(
                    TopologyManager.ShipmentTopicPath,
                    "expedited");

                var sender = new OrderSender(sendClient);
                var publisher = new ShipmentPublisher(topicClient);
                var subscriber = new ShipmentSubscriber(subscriptionClient);

                var processor = new OrderProcessor(
                    receiveClient,
                    order => Console.WriteLine($"Processed {order}"));

                subscriber.Start((order, carrier) =>
                {
                    Console.WriteLine($"Shipping {order.OrderId} via {carrier}.");
                    return Task.FromResult(0);
                });

                processor.Start();

                await sender.SendBatchAsync(BuildSampleOrders()).ConfigureAwait(false);

                await sender
                    .ScheduleAsync(BuildOrder("ORD-999", "west", 42m), DateTime.UtcNow.AddMinutes(5))
                    .ConfigureAwait(false);

                await publisher.PublishAsync(BuildOrder("ORD-1000", "east", 1200m), "fabrikam-air")
                    .ConfigureAwait(false);

                Console.WriteLine($"Queue depth: {await topology.GetQueueDepthAsync().ConfigureAwait(false)}");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();

                processor.Stop();
                subscriber.Stop();
                publisher.Close();
                sender.Close();
            }

            return 0;
        }

        private static IEnumerable<OrderMessage> BuildSampleOrders()
        {
            yield return BuildOrder("ORD-001", "west", 120.50m);
            yield return BuildOrder("ORD-002", "east", 2400.00m);
            yield return BuildOrder("ORD-003", "west", 89.99m);
        }

        private static OrderMessage BuildOrder(string orderId, string region, decimal total)
        {
            return new OrderMessage
            {
                OrderId = orderId,
                CustomerId = "CUST-" + orderId.Substring(orderId.Length - 3),
                Region = region,
                Total = total,
                PlacedOnUtc = DateTime.UtcNow,
            };
        }
    }
}

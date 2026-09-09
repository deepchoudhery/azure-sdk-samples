using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contoso.Ordering
{
    public static class Program
    {
        private const string ConnectionStringVariable = "CONTOSO_SERVICEBUS_CONNECTION";

        public static async Task<int> Main(string[] args)
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

            await using ServiceBusClientProvider provider =
                ServiceBusClientProvider.FromConnectionString(connectionString);
            await using var sender =
                new OrderSender(provider.CreateSender(TopologyManager.OrderQueuePath));
            await using var publisher =
                new ShipmentPublisher(provider.CreateSender(TopologyManager.ShipmentTopicPath));
            await using var subscriber = new ShipmentSubscriber(
                provider.Client,
                TopologyManager.ShipmentTopicPath,
                "expedited");
            await using var processor = new OrderProcessor(
                provider.Client,
                TopologyManager.OrderQueuePath,
                order => Console.WriteLine($"Processed {order}"));

            await subscriber
                .StartAsync((order, carrier) =>
                {
                    Console.WriteLine($"Shipping {order.OrderId} via {carrier}.");
                    return Task.FromResult(0);
                })
                .ConfigureAwait(false);

            await processor.Start().ConfigureAwait(false);
            await sender.SendBatchAsync(BuildSampleOrders()).ConfigureAwait(false);

            await sender
                .ScheduleAsync(BuildOrder("ORD-999", "west", 42m), DateTime.UtcNow.AddMinutes(5))
                .ConfigureAwait(false);

            await publisher
                .PublishAsync(BuildOrder("ORD-1000", "east", 1200m), "fabrikam-air")
                .ConfigureAwait(false);

            Console.WriteLine(
                $"Queue depth: {await topology.GetQueueDepthAsync().ConfigureAwait(false)}");
            Console.WriteLine("Press ENTER to stop.");
            Console.ReadLine();

            await processor.StopAsync().ConfigureAwait(false);
            await subscriber.StopAsync().ConfigureAwait(false);

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

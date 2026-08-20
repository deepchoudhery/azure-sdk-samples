using System;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;

namespace Contoso.Ordering
{
    /// <summary>
    /// Owns the single <see cref="ServiceBusClient"/> for the process.
    /// </summary>
    public sealed class ServiceBusClientProvider : IAsyncDisposable
    {
        private readonly ServiceBusClient _client;

        private ServiceBusClientProvider(ServiceBusClient client)
        {
            _client = client;
        }

        public ServiceBusClient Client
        {
            get { return _client; }
        }

        public static ServiceBusClientProvider FromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            }

            return new ServiceBusClientProvider(new ServiceBusClient(connectionString));
        }

        /// <summary>
        /// Creates a client from a namespace name or an absolute <c>sb://</c> namespace URI.
        /// </summary>
        public static ServiceBusClientProvider FromSharedAccessSignature(
            string serviceNamespace,
            string keyName,
            string sharedAccessKey)
        {
            string fullyQualifiedNamespace = NormalizeFullyQualifiedNamespace(serviceNamespace);
            var credential = new AzureNamedKeyCredential(keyName, sharedAccessKey);
            var options = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
            };
            options.RetryOptions.TryTimeout = TimeSpan.FromSeconds(60d);

            return new ServiceBusClientProvider(
                new ServiceBusClient(fullyQualifiedNamespace, credential, options));
        }

        public ServiceBusSender CreateSender(string path)
        {
            return _client.CreateSender(path);
        }

        public ServiceBusReceiver CreateReceiver(string path)
        {
            return _client.CreateReceiver(
                path,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                });
        }

        public ServiceBusProcessor CreateQueueProcessor(
            string path,
            int maxConcurrentCalls,
            TimeSpan maxAutoLockRenewalDuration)
        {
            return _client.CreateProcessor(
                path,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = maxConcurrentCalls,
                    MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration,
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                });
        }

        public ServiceBusProcessor CreateSubscriptionProcessor(
            string topicPath,
            string subscriptionName,
            int maxConcurrentCalls)
        {
            return _client.CreateProcessor(
                topicPath,
                subscriptionName,
                new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentCalls = maxConcurrentCalls,
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                });
        }

        public ValueTask DisposeAsync()
        {
            return _client.DisposeAsync();
        }

        private static string NormalizeFullyQualifiedNamespace(string serviceNamespace)
        {
            if (string.IsNullOrWhiteSpace(serviceNamespace))
            {
                throw new ArgumentException(
                    "A Service Bus namespace is required.",
                    nameof(serviceNamespace));
            }

            string candidate = serviceNamespace.Trim().TrimEnd('/');
            if (!candidate.Contains("://", StringComparison.Ordinal))
            {
                return candidate.Contains(".", StringComparison.Ordinal)
                    ? candidate
                    : $"{candidate}.servicebus.windows.net";
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri endpoint)
                || !string.Equals(endpoint.Scheme, "sb", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(endpoint.Host)
                || endpoint.AbsolutePath != "/")
            {
                throw new ArgumentException(
                    "The namespace must be a host name or an absolute sb:// namespace URI.",
                    nameof(serviceNamespace));
            }

            return endpoint.Host;
        }
    }
}

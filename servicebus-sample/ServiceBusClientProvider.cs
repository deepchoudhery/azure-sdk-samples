using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Contoso.Ordering
{
    /// <summary>
    /// Owns the single <see cref="ServiceBusClient"/> and its underlying AMQP connection.
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

        /// <summary>
        /// Standard connection-string path.
        /// </summary>
        public static ServiceBusClientProvider FromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            }

            return new ServiceBusClientProvider(
                new ServiceBusClient(connectionString, ServiceBusOperationPolicy.CreateClientOptions()));
        }

        /// <summary>
        /// Namespace plus an explicit shared-access key credential.
        /// </summary>
        public static ServiceBusClientProvider FromSharedAccessSignature(
            string serviceNamespace,
            string keyName,
            string sharedAccessKey)
        {
            if (string.IsNullOrWhiteSpace(serviceNamespace))
            {
                throw new ArgumentException("A Service Bus namespace is required.", nameof(serviceNamespace));
            }

            if (string.IsNullOrWhiteSpace(keyName))
            {
                throw new ArgumentException("A shared-access key name is required.", nameof(keyName));
            }

            if (string.IsNullOrWhiteSpace(sharedAccessKey))
            {
                throw new ArgumentException("A shared-access key is required.", nameof(sharedAccessKey));
            }

            string fullyQualifiedNamespace = NormalizeNamespace(serviceNamespace);
            var credential = new AzureNamedKeyCredential(keyName, sharedAccessKey);
            return new ServiceBusClientProvider(
                new ServiceBusClient(
                    fullyQualifiedNamespace,
                    credential,
                    ServiceBusOperationPolicy.CreateClientOptions()));
        }

        public ServiceBusSender CreateSender(string entityPath)
        {
            return _client.CreateSender(entityPath);
        }

        public ValueTask DisposeAsync()
        {
            return _client.DisposeAsync();
        }

        private static string NormalizeNamespace(string serviceNamespace)
        {
            string value = serviceNamespace.Trim().TrimEnd('/');
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri namespaceUri))
            {
                return namespaceUri.Host;
            }

            return value.Contains(".", StringComparison.Ordinal)
                ? value
                : value + ".servicebus.windows.net";
        }
    }

    /// <summary>
    /// Keeps the Track 1 retry budget and operation timeout explicit for every Track 2 client.
    /// </summary>
    internal static class ServiceBusOperationPolicy
    {
        internal const int MaxRetries = 10;
        internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

        internal static ServiceBusClientOptions CreateClientOptions()
        {
            return new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
                RetryOptions =
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = MaxRetries,
                    MaxDelay = MaxRetryDelay,
                    // TryTimeout applies to one attempt. The linked cancellation source below
                    // preserves the legacy 60-second deadline across the complete operation.
                    TryTimeout = OperationTimeout,
                },
            };
        }

        internal static ServiceBusAdministrationClientOptions CreateAdministrationClientOptions()
        {
            var options = new ServiceBusAdministrationClientOptions();
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.MaxRetries = MaxRetries;
            options.Retry.MaxDelay = MaxRetryDelay;
            options.Retry.NetworkTimeout = OperationTimeout;
            return options;
        }

        internal static CancellationTokenSource CreateCancellationSource(
            CancellationToken cancellationToken)
        {
            CancellationTokenSource source =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            source.CancelAfter(OperationTimeout);
            return source;
        }
    }
}

using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Contoso.Ordering
{
    /// <summary>
    /// Owns the single <see cref="MessagingFactory"/> for the process. The legacy SDK requires
    /// a factory before any entity client can be created, and the factory itself owns the
    /// underlying connection.
    /// </summary>
    public sealed class MessagingFactoryProvider : IDisposable
    {
        private readonly MessagingFactory _factory;

        private MessagingFactoryProvider(MessagingFactory factory)
        {
            _factory = factory;
        }

        public MessagingFactory Factory
        {
            get { return _factory; }
        }

        /// <summary>
        /// Standard connection-string path.
        /// </summary>
        public static MessagingFactoryProvider FromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            }

            MessagingFactory factory = MessagingFactory.CreateFromConnectionString(connectionString);
            return new MessagingFactoryProvider(factory);
        }

        /// <summary>
        /// Endpoint plus an explicit SAS token provider. This split form has no direct
        /// equivalent in the modern SDK.
        /// </summary>
        public static MessagingFactoryProvider FromSharedAccessSignature(
            string serviceNamespace,
            string keyName,
            string sharedAccessKey)
        {
            Uri address = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);

            TokenProvider tokenProvider =
                TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, sharedAccessKey);

            var settings = new MessagingFactorySettings
            {
                TokenProvider = tokenProvider,
                TransportType = TransportType.Amqp,
                OperationTimeout = TimeSpan.FromSeconds(60),
            };

            MessagingFactory factory = MessagingFactory.Create(address, settings);
            return new MessagingFactoryProvider(factory);
        }

        public QueueClient CreateQueueClient(string path)
        {
            return _factory.CreateQueueClient(path);
        }

        public QueueClient CreateQueueClient(string path, ReceiveMode mode)
        {
            return _factory.CreateQueueClient(path, mode);
        }

        public TopicClient CreateTopicClient(string path)
        {
            return _factory.CreateTopicClient(path);
        }

        public SubscriptionClient CreateSubscriptionClient(string topicPath, string subscriptionName)
        {
            return _factory.CreateSubscriptionClient(topicPath, subscriptionName, ReceiveMode.PeekLock);
        }

        public void Dispose()
        {
            if (_factory != null && !_factory.IsClosed)
            {
                _factory.Close();
            }
        }
    }
}

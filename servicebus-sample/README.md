# Service Bus sample — Azure.Messaging.ServiceBus on .NET 10

An order-processing console app: orders go onto a queue, shipments fan out through a topic
with filtered subscriptions, and the entity topology is created at startup.

- **TFM:** `net10.0`
- **Service Bus package:** `Azure.Messaging.ServiceBus`
- **Message body compatibility:** binary XML produced by `DataContractSerializer` is retained so
  existing queued messages and external producers/consumers remain compatible.

## What each file demonstrates

| File | Track 2 surface |
|------|---------------------------|
| `ServiceBusClientProvider.cs` | One long-lived `ServiceBusClient`, connection-string and explicit shared-key authentication, legacy-equivalent retry budget, AMQP/TCP, async disposal |
| `OrderSender.cs` | `ServiceBusMessage`, application properties, size-aware batches, scheduling, sessions, async sender disposal |
| `OrderProcessor.cs` | Peek-lock processors, manual settlement, bounded binary XML deserialization, pull receive, peek, error callbacks |
| `ShipmentPipeline.cs` | Topic sender and subscription processor with explicit complete/dead-letter handling |
| `TopologyManager.cs` | `ServiceBusAdministrationClient`, create options, SQL rules, and queue runtime properties |
| `OrderMessage.cs` | `[DataContract]` with bounded binary XML serialization and deserialization |

## Running

```powershell
dotnet build .\Contoso.Ordering.csproj
$env:CONTOSO_SERVICEBUS_CONNECTION = "Endpoint=sb://..."   # optional
dotnet run --project .\Contoso.Ordering.csproj
```

Without `CONTOSO_SERVICEBUS_CONNECTION` the app prints a notice and exits without touching
the network.

## Transport, retries, and timeouts

- Track 2 uses AMQP over TCP for both authentication paths. The legacy connection-string path
  used the Track 1 NetMessaging/SBMP default, so this is a deliberate transport change; the
  explicit shared-key path already selected AMQP. Firewalls that allowed only SBMP ports
  9350-9354 must also allow outbound AMQP ports 5671-5672. The topology administration calls
  use HTTPS on port 443; AMQP-over-WebSockets on port 443 is not selected for messaging.
- Both `ServiceBusClient` construction paths retain the Track 1 retry budget: exponential
  retries, 10 retry attempts, and a 30-second maximum retry delay. The administration client
  uses the same retry count and maximum backoff.
- Track 2 `TryTimeout` (and the administration client's network timeout) applies to each
  individual attempt, not to the complete logical operation. Every send, batch, schedule,
  receive, settlement, processor lifecycle, and topology operation therefore propagates a
  linked cancellation token with a 60-second total time budget across all attempts and
  sub-operations.

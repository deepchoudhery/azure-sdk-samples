# 01-upgrade-contoso-ordering: Upgrade and validate Contoso.Ordering on .NET 10

Verify the .NET 10 toolchain and upgrade `servicebus-sample/Contoso.Ordering.csproj` in place from `net472` to `net10.0`, retaining its SDK-style project format. Resolve dependency compatibility in the same atomic change, including replacing `WindowsAzure.ServiceBus` 6.2.1 with `Azure.Messaging.ServiceBus` and adapting the messaging code to the supported client, sender, and processor APIs.

Fix all source and behavioral compatibility findings identified by the assessment, including the flagged `TimeSpan` calls and `Uri` behavior, without deferred package or API stubs. Review the remaining package references for restore compatibility, then validate the completed migration with a clean restore, a zero-error build, and all available automated tests or executable smoke checks.

**Done when**: `Contoso.Ordering.csproj` targets `net10.0`, no incompatible `WindowsAzure.ServiceBus` reference remains, all assessed API incompatibilities are resolved, package restore succeeds without dependency conflicts, the project builds with zero errors, and all available automated validation passes.

## Research findings

- **Dependency model:** project-scoped MSBuild evaluation reports standard (non-CPM) package management, no `Directory.Build.props` or `Directory.Packages.props`, no project references, and one direct package defined in `servicebus-sample/Contoso.Ordering.csproj`: `WindowsAzure.ServiceBus` 6.2.1. Its 15 other packages are transitive.
- **Assessment summary/issues:** one incompatible package (`WindowsAzure.ServiceBus`), nine source-compatibility findings (`TimeSpan.FromDays`, `FromHours`, `FromMinutes`, and `FromSeconds` call sites), and one `System.Uri` behavioral finding. No additional technology feature was detected.
- **Package action:** remove `WindowsAzure.ServiceBus` 6.2.1 and add the latest stable package available from the configured feed, `Azure.Messaging.ServiceBus` 7.20.2. Administration types are included in that package; no separate administration package should be added.
- **Messaging migration:** replace `MessagingFactory` with one shared `ServiceBusClient`; migrate queue/topic senders, receivers, and callback pumps to `ServiceBusSender`, `ServiceBusReceiver`, and `ServiceBusProcessor`; migrate topology operations to `ServiceBusAdministrationClient`; preserve the explicit shared-access-key construction path with `AzureNamedKeyCredential`.
- **Behavior decisions:** use explicit JSON serialization/deserialization for the new message wire format; map `OperationTimeout` to retry `TryTimeout`; map AMQP to `AmqpTcp`; use explicit `DateTimeOffset` scheduling; validate and normalize namespace names/`sb://` endpoints instead of relying on legacy URI construction; use message-size-aware `ServiceBusMessageBatch` instances.
- **Validation plan:** use `dotnet restore` followed by a targeted `dotnet build` because the resulting project is SDK-style and targets only `net10.0`; run the executable without a connection string as its offline smoke check; verify no legacy Service Bus symbols or suppressed warnings remain.

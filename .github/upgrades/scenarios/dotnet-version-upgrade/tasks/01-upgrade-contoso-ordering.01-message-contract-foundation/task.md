# 01-upgrade-contoso-ordering.01-message-contract-foundation: Add the modern Service Bus SDK and shared message serialization foundation

## Objective
Establish a buildable side-by-side migration foundation for the Azure Service Bus data-plane rewrite without changing the target framework or removing the legacy package yet.

## Scope
- `servicebus-sample/Contoso.Ordering.csproj`
- `servicebus-sample/OrderMessage.cs`
- A focused serialization helper under `servicebus-sample/` if needed

## Steps
1. Add an explicit `Azure.Messaging.ServiceBus` 7.20.2 package reference while retaining `WindowsAzure.ServiceBus` 6.2.1 temporarily so subsequent concerns can migrate incrementally.
2. Define one consistent modern message-body serialization/deserialization approach for `OrderMessage` using `BinaryData` and System.Text.Json-compatible APIs. Keep message creation and body reading logic centralized so outbound and inbound code use the same wire format.
3. Update comments or annotations on `OrderMessage` only as needed to accurately describe the modern serialization contract; preserve its public data shape and behavior.
4. Restore and build the project on its existing `net472` target to verify that the side-by-side package state and shared helper compile before consumer migrations begin.

## Done when
The modern SDK is available alongside the legacy SDK, a single reusable `OrderMessage` serialization contract is implemented, and the unchanged `net472` project restores and builds with zero errors.

## Research Findings
- `servicebus-sample/Contoso.Ordering.csproj` is SDK-style and currently targets only `net472`; the target framework must remain unchanged in this subtask.
- Package management is project-local (no `Directory.Packages.props` or `Directory.Build.props` was found). The only direct package is `WindowsAzure.ServiceBus` 6.2.1, resolved at 6.2.1, so `Azure.Messaging.ServiceBus` 7.20.2 belongs beside it as a versioned `PackageReference`.
- The current restored legacy graph includes `Microsoft.Rest.ClientRuntime` 2.3.20, `Newtonsoft.Json` 10.0.3, and the other transitive dependencies of `WindowsAzure.ServiceBus`; none are removed by this foundation task.
- The assessment identifies `Azure.Messaging.ServiceBus` 7.20.2 as compatible and reports no package incompatibilities. The project has no project dependencies or dependants.
- `OrderMessage` is the shared payload used by `OrderSender`, `OrderProcessor`, and `ShipmentPipeline`. Existing legacy consumers construct `BrokeredMessage(order)` and read it with `GetBody<OrderMessage>()`, which uses the retained data-contract annotations and legacy binary XML wire format.
- The modern contract will use `BinaryData.FromObjectAsJson` and `BinaryData.ToObjectFromJson<OrderMessage>` through one helper. It will also centralize `ServiceBusMessage` creation and `ServiceBusReceivedMessage` body reading so future consumer migrations cannot accidentally choose a different wire format.
- No `// STUB:` markers were found in the scoped project, so stub decomposition is not required.
- Validation scope is the single affected project on `net472`; there are no test projects, and scenario instructions set test coverage to Skip.

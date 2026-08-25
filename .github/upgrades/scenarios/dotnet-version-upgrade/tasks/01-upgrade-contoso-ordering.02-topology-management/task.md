# 01-upgrade-contoso-ordering.02-topology-management: Migrate Service Bus topology administration to the modern SDK

## Objective
Replace the legacy `NamespaceManager` topology implementation with `ServiceBusAdministrationClient` while the project remains in the side-by-side package state.

## Scope
- `servicebus-sample/TopologyManager.cs`
- `servicebus-sample/Program.cs` only if construction or disposal wiring must change

## Steps
1. Replace `Microsoft.ServiceBus.NamespaceManager` and legacy queue/topic/subscription description types with `Azure.Messaging.ServiceBus.Administration` APIs.
2. Reimplement idempotent queue, topic, and subscription creation using the modern existence checks and create options. Preserve entity names, TTL, lock duration, delivery count, dead-lettering, duplicate detection, partitioning intent, and SQL filter behavior where supported.
3. Reimplement queue depth retrieval using runtime properties and preserve topology deletion behavior.
4. Use the appropriate modern response/value types and async APIs; resolve the assessment-reported `TimeSpan.FromDays` and `TimeSpan.FromMinutes` call sites in this file if the net10 overload analysis requires explicit numeric typing.
5. Verify there are no `Microsoft.ServiceBus` references left in `TopologyManager.cs`, then restore and build on `net472`.

## Research Findings
- **Project:** `servicebus-sample/Contoso.Ordering.csproj` is SDK-style, currently targets `net472`, has no project dependencies or dependants, and already references both `Azure.Messaging.ServiceBus` 7.20.2 and `WindowsAzure.ServiceBus` 6.2.1 for the staged migration.
- **Affected code:** `TopologyManager.cs` owns all topology administration. `Program.cs` only constructs `TopologyManager` from the existing connection string and does not require changes because `ServiceBusAdministrationClient` is not disposable.
- **Modern API mapping:** use `ServiceBusAdministrationClient` with `CreateQueueOptions`, `CreateTopicOptions`, and `CreateSubscriptionOptions`; existence checks return `Response<bool>` and runtime queue depth comes from `GetQueueRuntimePropertiesAsync(...).Value.ActiveMessageCount`.
- **Behavior preservation:** the modern create options support the existing queue/topic/subscription sizes, TTL, lock duration, delivery count, expiration dead-lettering, duplicate-detection window, partitioning, and batched-operation settings. Filtered subscriptions require a `CreateRuleOptions` named with `CreateRuleOptions.DefaultRuleName` and a `SqlRuleFilter`; an unfiltered subscription uses the default pass-through rule.
- **Assessment issues:** `TopologyManager.cs` contains five reported potential source incompatibilities involving `TimeSpan.FromDays(double)` and `TimeSpan.FromMinutes(double)`. Use explicit `double` literals in the migrated options so overload binding remains stable for the later `net10.0` cutover.
- **Package/dependency action:** no package edit is required in this subtask; `Azure.Messaging.ServiceBus` 7.20.2 contains the administration APIs and supports the intermediate `net472` build. The legacy package remains temporarily for the out-of-scope data-plane files.
- **Stubs:** no `// STUB:` markers were found in the project.
- **Validation:** restore and build `Contoso.Ordering.csproj` on `net472`, require zero warnings and errors, confirm the `net472` assembly exists, and verify `TopologyManager.cs` contains no `Microsoft.ServiceBus` or `NamespaceManager` usage.

## Done when
All topology and runtime-property operations use `ServiceBusAdministrationClient`, topology semantics are preserved, `TopologyManager.cs` has no legacy SDK usage, and the project builds with zero errors on the intermediate target.

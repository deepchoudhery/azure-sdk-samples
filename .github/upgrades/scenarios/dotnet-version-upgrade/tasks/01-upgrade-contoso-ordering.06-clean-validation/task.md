# 01-upgrade-contoso-ordering.06-clean-validation: Run final clean restore and build validation for the upgraded application

## Objective
Validate the completed .NET 10 and Azure Service Bus SDK migration from a clean state before allowing the parent task to complete.

## Scope
- Entire `servicebus-sample/Contoso.Ordering.csproj` and its source files
- Generated restore/build outputs only as validation artifacts

## Research findings
- `Contoso.Ordering.csproj` is SDK-style and declares the single target framework `net10.0`; no
  `TargetFrameworks` fallback is present. The installed stable .NET 10 SDK is available, so
  `dotnet restore` and `dotnet build` are the appropriate validation tools.
- The only direct package is `Azure.Messaging.ServiceBus` 7.20.2. An exact package-source lookup
  confirms 7.20.2 is the newest stable version available from the configured `azure-default`
  source, and the current `net10.0` dependency graph resolves it exactly without a downgrade.
- Source inventory found Track 2 `Azure.Messaging.ServiceBus` and
  `Azure.Messaging.ServiceBus.Administration` APIs only. Searches for Track 1 namespaces, packages,
  and representative types (`Microsoft.ServiceBus`, `WindowsAzure.ServiceBus`,
  `Microsoft.Azure.ServiceBus`, `BrokeredMessage`, `MessagingFactory`, and legacy sender/receiver
  types) returned no matches.
- No deferred implementation markers or compilation stubs were found. Matches containing the word
  `Exception` are concrete error handling, not `NotImplementedException` or placeholder code.
- `Program.RunAsync` reads `CONTOSO_SERVICEBUS_CONNECTION` before constructing any Service Bus
  client and returns success when it is absent, providing a credential-free startup validation path.
- Existing `bin` and `obj` trees contain stale `net472` artifacts from before the in-place upgrade.
  Remove both generated trees before restore so final evidence cannot accidentally come from an old
  target or incremental build.
- Final review identified a deployment-compatibility gap: queues and subscriptions can retain up to
  seven days of payloads emitted by `BrokeredMessage(OrderMessage)`. Those bodies are Data Contract
  binary XML, not JSON, so JSON-only reads would reject valid in-flight work after deployment.
- Compatibility must remain narrow: attempt the current JSON contract first, fall back only for
  `JsonException`, use the platform `DataContractSerializer` plus a quota-limited binary XML reader
  (without restoring the Track 1 package), cap legacy bodies at 1 MiB, and let invalid legacy XML fail
  as a serialization error rather than masking it.

## Steps
1. Verify `Contoso.Ordering.csproj` has exactly one `TargetFramework` value (`net10.0`), references
   `Azure.Messaging.ServiceBus` 7.20.2, and has no direct legacy Service Bus or Track 1 runtime package.
2. Search all project-owned source and project metadata (excluding generated outputs) for legacy
   Service Bus namespaces, packages, and types; also search for TODO/FIXME markers,
   `NotImplementedException`, and placeholder/stub implementations.
3. Delete the project-local `bin` and `obj` directories, perform an explicit fresh
   `dotnet restore` for `net10.0`, and inspect the direct/transitive dependency graph for NU1605,
   legacy SDK packages, and unintended framework fallback.
4. Run `dotnet build servicebus-sample/Contoso.Ordering.csproj -c Release -f net10.0
   --no-restore --no-incremental` and require zero warnings and zero errors. Confirm the resulting
   assembly and runtime configuration exist only under `bin/Release/net10.0`.
5. Run the built application with `CONTOSO_SERVICEBUS_CONNECTION` removed from the process
   environment. Require exit code 0 and the explicit no-contact message, proving startup does not
   construct a client or contact Azure without configuration.
6. Re-run residue and stub searches after any correction. Test-baseline generation and test execution
   remain out of scope because the scenario explicitly selected Test Coverage: Skip.
7. Exercise `OrderMessageContract.Deserialize` locally with the current JSON body, a representative
   `DataContractSerializer` binary XML body matching `BrokeredMessage(OrderMessage)`, and malformed
   bytes. Require both valid formats to preserve all fields and malformed input to throw explicitly.

## Done when
A clean restore and Release build of the entire `net10.0` application succeed with zero errors,
package and source searches show no legacy SDK residue, both retained binary XML and current JSON
payloads deserialize correctly under the bounded compatibility policy, and no unresolved
compatibility work or compilation stubs remain.

# Clean validation progress

## Status

**Completed.** The upgraded `Contoso.Ordering` application passes clean .NET 10 validation,
including bounded compatibility for retained legacy message bodies.

## Research and inventory

- Read the complete task, scenario execution constraints, and the requested build and Azure SDK
  migration skills before acting.
- Enriched `task.md` with the observed project/package state and an exact clean-validation procedure
  before making any possible source correction.
- Confirmed `Contoso.Ordering.csproj` is SDK-style and declares exactly one
  `<TargetFramework>net10.0</TargetFramework>`; it has no `TargetFrameworks` fallback.
- Confirmed the only direct package is `Azure.Messaging.ServiceBus` 7.20.2. An exact search of the
  configured package sources reports 7.20.2 as the newest available stable release.
- Confirmed the fresh `net10.0` dependency graph resolves the requested package exactly. It contains
  no `WindowsAzure.ServiceBus`, `Microsoft.Azure.ServiceBus`, `Microsoft.ServiceBus`,
  `Microsoft.Rest.*`, or App Authentication package. `Microsoft.Azure.Amqp` 2.7.0 is an expected
  transitive implementation dependency of the current Track 2 client, not the legacy Service Bus SDK.
- Confirmed the configured package sources report no deprecated direct or transitive packages.
- Searched all project-owned C# and project metadata for legacy Service Bus namespaces and
  representative Track 1 types; no matches were found.
- Searched all project-owned C# for TODO/FIXME markers, placeholder/stub text,
  `NotImplementedException`, `NotSupportedException`, and generic `throw new Exception`; no matches
  were found.
- Final code review found that `OrderMessageContract.Deserialize` accepted only the new JSON body.
  Since Service Bus entities can retain pre-deployment messages for up to seven days and the former
  `BrokeredMessage(OrderMessage)` path emitted Data Contract binary XML, valid in-flight messages
  would otherwise have retried and eventually dead-lettered.

## Final review correction

- Kept JSON as the primary wire format and catches only `JsonException` before attempting legacy
  decoding; unsupported serializer configuration and unrelated failures are not hidden.
- Added a package-free legacy reader using `DataContractSerializer` and
  `XmlDictionaryReader.CreateBinaryReader`.
- Bounded the compatibility path to a 1 MiB body, 16 graph items, depth 16, 1 MiB string content,
  4 KiB reads, 4 KiB name tables, and 16-element arrays. It verifies the expected contract root,
  requires an `OrderMessage`, and rejects trailing content. Malformed payloads continue to surface
  explicit `SerializationException`/XML failures.

## Clean validation

1. Removed `servicebus-sample/bin` and `servicebus-sample/obj`, including stale pre-migration
   `net472` artifacts.
2. Ran:

   ```text
   dotnet restore .\servicebus-sample\Contoso.Ordering.csproj --force --no-cache -p:TargetFramework=net10.0 --verbosity minimal
   ```

   Result: restore succeeded.

3. Ran:

   ```text
   dotnet build .\servicebus-sample\Contoso.Ordering.csproj -c Release -f net10.0 --no-restore --no-incremental --verbosity minimal
   ```

   Result: build succeeded with **0 warnings and 0 errors**. No NU dependency warning or conflict was
   emitted.

4. Verified:
   - `bin\Release\net10.0\Contoso.Ordering.dll` exists.
   - `bin\Release\net10.0\Contoso.Ordering.runtimeconfig.json` exists.
   - No `net472` directory remains under project-local `bin` or `obj`.
   - Direct package requested/resolved: `Azure.Messaging.ServiceBus` 7.20.2 / 7.20.2.

5. Removed `CONTOSO_SERVICEBUS_CONNECTION` from the validation process and ran the built assembly.
   It printed the configuration guidance and `Nothing to do — exiting without contacting Azure.`,
   then exited with code 0. No Azure credentials or network connection were required.

### Validation after the final review correction

- Deleted `servicebus-sample/bin` and `servicebus-sample/obj` again, then ran the same forced,
  no-cache `net10.0` restore. Restore succeeded.
- Re-ran the Release `net10.0` build with `--no-restore --no-incremental`. Build succeeded with
  **0 warnings and 0 errors**.
- Loaded the built assembly in-process and round-tripped all representative fields through both:
  1. `OrderMessageContract.Serialize` / the current JSON reader; and
  2. `DataContractSerializer` over `XmlDictionaryWriter.CreateBinaryWriter`, matching the former
     `BrokeredMessage(OrderMessage)` body, into the compatibility reader.
- Passed both round trips and confirmed malformed bytes fail with an explicit
  `SerializationException`/XML-format error. The focused check used no Service Bus package from the
  legacy SDK and made no network or Azure connection.

### Blocking review corrections

- `OrderMessageContract.Deserialize` now rejects a valid JSON `null` body explicitly with
  `SerializationException` rather than returning `null`.
- The bounded legacy reader catches only `XmlException` from binary-XML processing and wraps it in
  `SerializationException`, preserving the XML failure as `InnerException`. This ensures malformed
  legacy bodies reach the processor's serialization-failure dead-letter path without hiding
  unrelated exceptions.
- Deleted project-local `bin` and `obj`, then ran the forced no-cache restore and Release
  `net10.0` no-restore/no-incremental build again. Restore succeeded; build succeeded with
  **0 warnings and 0 errors**.
- Focused in-memory checks passed for JSON round-trip, legacy Data Contract binary-XML round-trip,
  JSON `null` rejection as `SerializationException`, and malformed binary-XML rejection as
  `SerializationException` with an `XmlException` inner exception.

## Scope notes

- No tests were generated or run because scenario Test Coverage is `Skip`.
- No live Azure integration was attempted or required.
- No project or package correction was necessary; `OrderMessageContract.cs` received the bounded
  compatibility correction described above.
- Focused in-memory validation covered the current JSON representation, a representative legacy
  Data Contract binary XML body, and malformed bytes without connecting to Azure.

## Files modified

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-upgrade-contoso-ordering.06-clean-validation/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-upgrade-contoso-ordering.06-clean-validation/progress-details.md`
- `servicebus-sample/OrderMessageContract.cs`

Project-local `bin` and `obj` were deleted and regenerated solely as ignored validation artifacts.

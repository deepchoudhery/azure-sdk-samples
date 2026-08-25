# Progress Details

## Status

Completed `01-upgrade-contoso-ordering.01-message-contract-foundation`.

## Research

- Confirmed `servicebus-sample/Contoso.Ordering.csproj` is an SDK-style executable targeting only `net472`.
- Confirmed package versions are declared directly in the project; no central package management files apply.
- Before changes, the only direct package was `WindowsAzure.ServiceBus` 6.2.1, resolved at the requested version.
- The legacy payload flow constructs `BrokeredMessage` from `OrderMessage` and reads it with `GetBody<OrderMessage>()` in the queue and topic consumers.
- The assessment reports `Azure.Messaging.ServiceBus` 7.20.2 as compatible and reports no package incompatibilities for the project.
- No `// STUB:` markers were present.
- Recorded these findings in `task.md` before editing project or source files.

## Implementation

- Added a direct `Azure.Messaging.ServiceBus` 7.20.2 package reference while retaining `WindowsAzure.ServiceBus` 6.2.1.
- Added `OrderMessageContract`, the single modern JSON wire-format boundary for `OrderMessage`.
  - `CreateMessage` constructs a `ServiceBusMessage` from the shared serializer and sets `ContentType` to `application/json`.
  - `Read` extracts and deserializes a `ServiceBusReceivedMessage` body through the same contract.
  - `Serialize` uses `BinaryData.FromObjectAsJson`.
  - `Deserialize` uses `BinaryData.ToObjectFromJson<OrderMessage>`.
  - Public entry points reject null arguments consistently.
- Updated the `OrderMessage` summary to describe the modern JSON contract while retaining all existing public properties and data-contract annotations for the still-active legacy path.
- Kept the target framework at `net472`; no legacy producer or consumer was migrated in this foundation subtask.

## Validation

Executed:

```text
dotnet restore .\servicebus-sample\Contoso.Ordering.csproj --verbosity minimal
dotnet build .\servicebus-sample\Contoso.Ordering.csproj --configuration Release --no-restore --verbosity minimal
```

Results:

- Restore succeeded.
- Release build succeeded for `net472`.
- Build result: **0 warnings, 0 errors**.
- Output verified at `servicebus-sample\bin\Release\net472\Contoso.Ordering.exe`.
- Package graph verification shows both requested direct packages resolved side by side:
  - `Azure.Messaging.ServiceBus` requested/resolved `7.20.2`
  - `WindowsAzure.ServiceBus` requested/resolved `6.2.1`
- The modern graph resolves `System.Text.Json` 10.0.9 and `System.Memory.Data` 10.0.9 transitively, providing the JSON-compatible `BinaryData` APIs used by the contract.

No automated test project exists, and scenario instructions set test coverage to Skip. No live Azure Service Bus interoperability check was attempted because this subtask only establishes the buildable contract foundation and no service credentials are part of the task.

## Files Modified

- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.01-message-contract-foundation\task.md`
- `.github\upgrades\scenarios\dotnet-version-upgrade\tasks\01-upgrade-contoso-ordering.01-message-contract-foundation\progress-details.md`
- `servicebus-sample\Contoso.Ordering.csproj`
- `servicebus-sample\OrderMessage.cs`
- `servicebus-sample\OrderMessageContract.cs`

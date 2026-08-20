# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade `servicebus-sample/Contoso.Ordering.csproj` from `net472` to `net10.0` in place.
**Scope**: Small, independent SDK-style application project with 7 code files, 16 NuGet packages, and no project-to-project dependencies.

### Selected Strategy
**All-at-Once** — All project changes are applied in a single atomic operation.
**Rationale**: The repository scope contains one independent project with no dependency graph to phase.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | A single project with no dependencies does not benefit from incremental ordering. |
| Project Approach | In-place | The SDK-style project can move directly from `net472` to `net10.0` without a parallel project. |
| Unsupported Packages | Resolve Inline | The assessment identifies one incompatible package with a known modern replacement. |
| Unsupported API Handling | Fix Inline | The limited API compatibility findings can be resolved within the atomic upgrade without deferred stubs. |

## Tasks

### 01-upgrade-contoso-ordering: Upgrade and validate Contoso.Ordering on .NET 10

Verify the .NET 10 toolchain and upgrade `servicebus-sample/Contoso.Ordering.csproj` in place from `net472` to `net10.0`, retaining its SDK-style project format. Resolve dependency compatibility in the same atomic change, including replacing `WindowsAzure.ServiceBus` 6.2.1 with `Azure.Messaging.ServiceBus` and adapting the messaging code to the supported client, sender, and processor APIs.

Fix all source and behavioral compatibility findings identified by the assessment, including the flagged `TimeSpan` calls and `Uri` behavior, without deferred package or API stubs. Review the remaining package references for restore compatibility, then validate the completed migration with a clean restore, a zero-error build, and all available automated tests or executable smoke checks.

**Done when**: `Contoso.Ordering.csproj` targets `net10.0`, no incompatible `WindowsAzure.ServiceBus` reference remains, all assessed API incompatibilities are resolved, package restore succeeds without dependency conflicts, the project builds with zero errors, and all available automated validation passes.

# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
  - [Binding Redirect Configuration](#binding-redirect-configuration)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [Contoso.Documents.csproj](#contosodocumentscsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 85 | All compatible |
| Total Code Files | 7 |  |
| Total Code Files with Incidents | 4 |  |
| Total Lines of Code | 843 |  |
| Total Number of Issues | 10 |  |
| Estimated LOC to modify | 9+ | at least 1.1% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Test Coverage | Package Issues | API Issues | Binding Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| [Contoso.Documents.csproj](#contosodocumentscsproj) | net8.0 | 🟢 Low | 🧪 Recommended | 0 | 9 | 0 | 9+ | DotNetCoreApp, Sdk Style = True |

🧪 **Test Coverage** — projects risky enough to add behavior-locking tests before upgrading, to catch regressions the upgrade may introduce. Requires the **dotnet-test** plugin.

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 85 | 100.0% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 0 | 0.0% |
| ***Total NuGet Packages*** | ***85*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 5 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 4 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 911 |  |
| ***Total APIs Analyzed*** | ***920*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Microsoft.CSharp | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| Microsoft.NETCore.Platforms | 1.1.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| Microsoft.NETCore.Targets | 1.1.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| Microsoft.Win32.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| NETStandard.Library | 1.6.1 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| Newtonsoft.Json | 10.0.2 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.fedora.23-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.fedora.24-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.native.System | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.native.System.IO.Compression | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.native.System.Net.Http | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.native.System.Security.Cryptography.Apple | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.opensuse.13.2-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.opensuse.42.1-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.osx.10.10-x64.runtime.native.System.Security.Cryptography.Apple | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.osx.10.10-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.rhel.7-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.ubuntu.14.04-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.ubuntu.16.04-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| runtime.ubuntu.16.10-x64.runtime.native.System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.AppContext | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Buffers | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Collections | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Collections.Concurrent | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Collections.NonGeneric | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Collections.Specialized | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.ComponentModel | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.ComponentModel.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.ComponentModel.TypeConverter | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Console | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Diagnostics.Debug | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Diagnostics.DiagnosticSource | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Diagnostics.Tools | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Diagnostics.Tracing | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Dynamic.Runtime | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Globalization | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Globalization.Calendars | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Globalization.Extensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.IO | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.IO.Compression | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.IO.Compression.ZipFile | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.IO.FileSystem | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.IO.FileSystem.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Linq | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Linq.Expressions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Net.Http | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Net.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Net.Sockets | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.ObjectModel | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.Emit | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.Emit.ILGeneration | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.Emit.Lightweight | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.Extensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Reflection.TypeExtensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Resources.ResourceManager | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.Extensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.Handles | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.InteropServices | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.InteropServices.RuntimeInformation | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.Numerics | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.Serialization.Formatters | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Runtime.Serialization.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.Algorithms | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.Cng | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.Csp | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.Encoding | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.OpenSsl | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.Primitives | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Security.Cryptography.X509Certificates | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Text.Encoding | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Text.Encoding.Extensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Text.RegularExpressions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Threading | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Threading.Tasks | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Threading.Tasks.Extensions | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Threading.Timer | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Xml.ReaderWriter | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Xml.XDocument | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| System.Xml.XmlDocument | 4.3.0 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |
| WindowsAzure.Storage | 9.3.3 |  | [Contoso.Documents.csproj](#contosodocumentscsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| T:System.Uri | 4 | 44.4% | Behavioral Change |
| M:System.TimeSpan.FromSeconds(System.Double) | 2 | 22.2% | Source Incompatible |
| M:System.TimeSpan.FromMinutes(System.Double) | 2 | 22.2% | Source Incompatible |
| M:System.TimeSpan.FromDays(System.Double) | 1 | 11.1% | Source Incompatible |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;Contoso.Documents.csproj</b><br/><small>net8.0</small>"]
    click P1 "#contosodocumentscsproj"

```

## Project Details

<a id="contosodocumentscsproj"></a>
### Contoso.Documents.csproj

#### Project Info

- **Current Target Framework:** net8.0
- **Proposed Target Framework:** net10.0
- **SDK-style**: True
- **Project Kind:** DotNetCoreApp
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 7
- **Number of Files with Incidents**: 4
- **Lines of Code**: 843
- **Estimated LOC to modify**: 9+ (at least 1.1% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["Contoso.Documents.csproj"]
        MAIN["<b>📦&nbsp;Contoso.Documents.csproj</b><br/><small>net8.0</small>"]
        click MAIN "#contosodocumentscsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 5 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 4 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 911 |  |
| ***Total APIs Analyzed*** | ***920*** |  |


# 02-upgrade-contoso-documents: Upgrade the storage sample to .NET 10

Upgrade `storage-sample\Contoso.Documents.csproj` and any generated baseline test project together in one atomic pass. Verify the .NET 10 SDK and any `global.json` constraints, update target frameworks, apply Azure SDK-appropriate package and API changes, restore dependencies, and resolve the seven assessed compatibility incidents while preserving the behavior captured by the baseline.

The assessment reports one compatible but deprecated `WindowsAzure.Storage` 9.3.3 dependency, five source-incompatible `TimeSpan.From*` call sites, and two behavioral `System.Uri` incidents. Research the supported package/API path before changing the dependency, retain the sample's intended behavior, and avoid unrelated modernization.

**Done when**: All scoped production and generated test projects target `net10.0`, restore succeeds, and the scoped workspace builds with zero errors.

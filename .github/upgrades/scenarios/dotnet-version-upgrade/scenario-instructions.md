# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0
- **Project Scope**: keyvault-sample\Contoso.Secrets.csproj
- **Azure SDK Guidance**: Use appropriate Azure SDK upgrade skills
- **Azure SDK Migration Skill**: Apply `migrating-azure-sdk-to-track2` and retain its four-shape behavior audit
- **Infrastructure Changes**: Do not deploy or delete infrastructure
- **Live Azure Access**: Do not use production credentials or run live Azure commands
- **Pull Requests**: Do not create a pull request in this run
- **Repository Scope**: Do not modify sibling samples, shared plugin files, global settings, or harness files

## Source Control
- **Source Branch**: deepchoudhery-key-vault-net10-trial
- **Working Branch**: deepchoudhery-key-vault-net10-trial
- **Commit Strategy**: Manual
- **Branch Sync**: Disabled

## Decisions
- **Upgrade Strategy**: All-at-Once
- **Test Coverage**: Skip generated coverage; retain existing build and test validation
- **Validation**: Build only the scoped project and run affected existing tests without live Azure access
- **Publication**: Leave commits and final publication to the outer supervisor

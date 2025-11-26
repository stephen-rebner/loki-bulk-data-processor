# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade LokiBulkDataProcessor.Core\LokiBulkDataProcessor.Core.csproj
4. Upgrade LokiBulkDataProcessor\LokiBulkDataProcessor.csproj
5. Upgrade LokiBulkDataProcessor.Core.UnitTests\LokiBulkDataProcessor.Core.UnitTests.csproj
6. Upgrade LokiBulkDataProcessor.ComosDb\LokiBulkDataProcessor.ComosDb.csproj
7. Upgrade LokiBulkDataProcessor.IntegrationTests\LokiBulkDataProcessor.IntegrationTests.csproj
8. Upgrade LokiBulkDataProcessor.UnitTests\LokiBulkDataProcessor.UnitTests.csproj
9. Run unit tests to validate upgrade in the projects listed below:
   - LokiBulkDataProcessor.Core.UnitTests\LokiBulkDataProcessor.Core.UnitTests.csproj
   - LokiBulkDataProcessor.UnitTests\LokiBulkDataProcessor.UnitTests.csproj
   - LokiBulkDataProcessor.IntegrationTests\LokiBulkDataProcessor.IntegrationTests.csproj

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                              | Current Version | New Version | Description                          |
|:------------------------------------------|:---------------:|:-----------:|:-------------------------------------|
| Microsoft.EntityFrameworkCore.Design      | 8.0.12          | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.EntityFrameworkCore.SqlServer   | 8.0.12          | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.EntityFrameworkCore.Tools       | 8.0.12          | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.Extensions.Configuration        | 8.0.0           | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.Extensions.DependencyInjection  | 8.0.1           | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.Extensions.Logging              | 8.0.1           | 10.0.0      | Recommended for .NET 10.0            |
| Microsoft.Extensions.Logging.Console      | 8.0.1           | 10.0.0      | Recommended for .NET 10.0            |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### LokiBulkDataProcessor.Core\LokiBulkDataProcessor.Core.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging should be updated from `8.0.1` to `10.0.0` (*recommended for .NET 10.0*)

#### LokiBulkDataProcessor\LokiBulkDataProcessor.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.DependencyInjection should be updated from `8.0.1` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging should be updated from `8.0.1` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Logging.Console should be updated from `8.0.1` to `10.0.0` (*recommended for .NET 10.0*)

#### LokiBulkDataProcessor.Core.UnitTests\LokiBulkDataProcessor.Core.UnitTests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### LokiBulkDataProcessor.ComosDb\LokiBulkDataProcessor.ComosDb.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### LokiBulkDataProcessor.IntegrationTests\LokiBulkDataProcessor.IntegrationTests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore.Design should be updated from `8.0.12` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.SqlServer should be updated from `8.0.12` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.EntityFrameworkCore.Tools should be updated from `8.0.12` to `10.0.0` (*recommended for .NET 10.0*)
  - Microsoft.Extensions.Configuration should be updated from `8.0.0` to `10.0.0` (*recommended for .NET 10.0*)

#### LokiBulkDataProcessor.UnitTests\LokiBulkDataProcessor.UnitTests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

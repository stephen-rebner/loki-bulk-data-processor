# LokiBulkDataProcessor.Core - Public API Surface

This document outlines what is exposed publicly vs. kept internal in the Core project.

## ? Public API (Exposed to NuGet Consumers)

### Interfaces (Public Contract)
- `IBulkProcessor` - Main processor interface
- `IAppContext` - Context configuration interface
- `IModelMappingCollection` - Model mapping collection contract
- `IDataMappingCollection` - Data mapping collection contract
- `IToDestination` - Fluent mapping destination interface
- `IMapDataTableSource` - Data table mapping interface

### Base Classes for User Extension
- `AbstractMapping` - Base mapping class
- `ModelMapping<TSource>` - Users inherit to create model mappings
- `DataMapping` - Users inherit to create data table mappings
- `AbstractModelMapping` - Base for model mappings

### Exceptions
- `MappingException` - Thrown when mapping configuration is invalid

---

## ?? Internal API (Hidden from NuGet Consumers)

These types are accessible to:
- `LokiBulkDataProcessor` (main package)
- `LokiBulkDataProcessor.CosmosDb` (future package)
- `LokiBulkDataProcessor.IntegrationTests`
- `LokiBulkDataProcessor.UnitTests`

### Context Implementation
- `AppContext` - Internal implementation of IAppContext

### Mapping Internals
- `MappingMetaData` - Internal mapping metadata structure
- `AbstractMappingInfo` - Internal base mapping information
- `ModelMappingInfo<TSource>` - Internal model mapping logic
- `DataMappingInfo` - Internal data table mapping logic

### Configuration
- `DefaultConfigValues` - Internal default configuration values

---

## How This Works

The `InternalsVisibleTo` attribute in `LokiBulkDataProcessor.Core.csproj` allows specific assemblies to access internal types:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="LokiBulkDataProcessor" />
  <InternalsVisibleTo Include="LokiBulkDataProcessor.CosmosDb" />
  <InternalsVisibleTo Include="LokiBulkDataProcessor.IntegrationTests" />
  <InternalsVisibleTo Include="LokiBulkDataProcessor.UnitTests" />
</ItemGroup>
```

## Benefits

1. **Clean API Surface**: NuGet consumers only see `IBulkProcessor` and extension points
2. **Implementation Hiding**: Internal implementation details are not exposed
3. **Flexibility**: You can refactor internals without breaking consumers
4. **Code Sharing**: Core logic is shared between SQL Server and CosmosDb packages
5. **Type Safety**: Full compile-time checking within your solution

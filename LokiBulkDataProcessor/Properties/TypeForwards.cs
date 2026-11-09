// These public types moved to LokiBulkDataProcessor.Core in 9.0.0 with unchanged namespaces.
// The forwarders let assemblies compiled against 8.x (for example a separate "Mappings" library that
// only subclasses ModelMapping<T> / DataMapping) bind to Core at runtime. Keep them through 9.x.
using System.Runtime.CompilerServices;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Logging;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;

[assembly: TypeForwardedTo(typeof(IBulkProcessor))]
[assembly: TypeForwardedTo(typeof(ModelMapping<>))]
[assembly: TypeForwardedTo(typeof(AbstractModelMapping))]
[assembly: TypeForwardedTo(typeof(DataMapping))]
[assembly: TypeForwardedTo(typeof(AbstractMapping))]
[assembly: TypeForwardedTo(typeof(IToDestination))]
[assembly: TypeForwardedTo(typeof(IAsPrimaryKey))]
[assembly: TypeForwardedTo(typeof(IMapDataTableSource))]
[assembly: TypeForwardedTo(typeof(IModelMapping))]
[assembly: TypeForwardedTo(typeof(LokiLoggingOptions))]
// Public in 8.x and still public in step 3; step 4 makes them internal and drops these forwarders.
[assembly: TypeForwardedTo(typeof(IDataMappingCollection))]
[assembly: TypeForwardedTo(typeof(IModelMappingCollection))]
[assembly: TypeForwardedTo(typeof(AbstractMappings<>))]

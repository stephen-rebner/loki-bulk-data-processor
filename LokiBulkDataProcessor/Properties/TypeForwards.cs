// These public types moved to LokiBulkDataProcessor.Core in 9.0.0 with unchanged namespaces.
// The forwarders let assemblies compiled against 8.x (for example a separate "Mappings" library that
// only subclasses ModelMapping<T> / DataMapping) bind to Core at runtime. Keep them through 9.x.
// IDataMappingCollection, IModelMappingCollection and AbstractMappings<T> became internal in 9.0.0, so they are not forwarded.
using System.Runtime.CompilerServices;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Logging;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.Mappings.Interfaces;

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

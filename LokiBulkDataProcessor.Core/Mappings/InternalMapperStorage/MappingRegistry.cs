using System;
using System.Collections.Generic;
using System.Reflection;
using Loki.BulkDataProcessor.Mappings.Interfaces;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage;

/// <summary>
/// Immutable, app-wide set of model and data mappings (singleton). Holds no per-call state (#11).
/// </summary>
internal sealed class MappingRegistry
{
    /// <summary>Discovers the mappings in <paramref name="assemblies"/>.</summary>
    /// <exception cref="InvalidOperationException">A mapping fails to instantiate, or two mappings share a key.</exception>
    public MappingRegistry(IEnumerable<Assembly> assemblies)
        : this(new ModelMappings(assemblies), new DataMappings(assemblies))
    {
    }

    internal MappingRegistry(IModelMappingCollection models, IDataMappingCollection data)
    {
        Models = models;
        Data = data;
    }

    /// <summary>A registry with no mappings: every source is mapped by name 1:1.</summary>
    public static MappingRegistry Empty { get; } = new(Array.Empty<Assembly>());

    public IModelMappingCollection Models { get; }

    public IDataMappingCollection Data { get; }
}

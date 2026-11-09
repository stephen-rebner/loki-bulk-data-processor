using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    /// <summary>
    /// Holds the data mappings (<see cref="DataMapping"/>) discovered in the mapping assemblies.
    /// Immutable after construction.
    /// </summary>
    internal class DataMappings : AbstractMappings<DataMapping>, IDataMappingCollection
    {
        /// <summary>Instantiates every concrete <see cref="DataMapping"/> in <paramref name="mappingAssemblies"/>.</summary>
        /// <exception cref="InvalidOperationException">Two mappings share a <see cref="DataMapping.SourceTableName"/>.</exception>
        public DataMappings(IEnumerable<Assembly> mappingAssemblies)
            : this(FindMappingTypes(mappingAssemblies))
        {
        }

        /// <summary>Instantiates the given mapping types (tests use this to avoid scanning a whole assembly).</summary>
        internal DataMappings(IEnumerable<Type> mappingTypes)
        {
            AddMappings(mappingTypes);
            ThrowIfDuplicateKeys(mapping => mapping.SourceTableName, StringComparer.Ordinal, "the source table name");
        }

        /// <summary>Returns the mapping whose SourceTableName equals <paramref name="sourceName"/> (ordinal), else null.</summary>
        public DataMapping GetMappingFor(string sourceName)
        {
            return _mappings.FirstOrDefault(mapping => string.Equals(mapping.SourceTableName, sourceName, StringComparison.Ordinal));
        }
    }
}

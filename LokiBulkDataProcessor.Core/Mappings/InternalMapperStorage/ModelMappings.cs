using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    /// <summary>
    /// Holds the model mappings (<see cref="ModelMapping{TSource}"/>) discovered in the mapping assemblies.
    /// Immutable after construction.
    /// </summary>
    internal class ModelMappings : AbstractMappings<AbstractModelMapping>, IModelMappingCollection
    {
        /// <summary>Instantiates every concrete model mapping in <paramref name="mappingAssemblies"/>.</summary>
        /// <exception cref="InvalidOperationException">Two mappings target the same model type.</exception>
        public ModelMappings(IEnumerable<Assembly> mappingAssemblies)
            : this(FindMappingTypes(mappingAssemblies))
        {
        }

        /// <summary>Instantiates the given mapping types (tests use this to avoid scanning a whole assembly).</summary>
        internal ModelMappings(IEnumerable<Type> mappingTypes)
        {
            AddMappings(mappingTypes);
            ThrowIfDuplicateKeys(mapping => mapping.SourceType, EqualityComparer<Type>.Default, "the model type");
        }

        /// <summary>Returns the mapping for <paramref name="sourceType"/>, else null.</summary>
        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }
    }
}

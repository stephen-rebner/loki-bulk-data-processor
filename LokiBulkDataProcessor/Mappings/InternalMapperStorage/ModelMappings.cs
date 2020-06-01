using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    /// <summary>
    /// A class that holds a collection of model mappings
    /// The mappings are added dynamically when instantiated
    /// </summary>
    internal class ModelMappings : AbstractMappings<AbstractModelMapping>, IModelMappingCollection
    {
        /// <summary>
        /// Adds all Model mappings contained in the assembly provided
        /// </summary>
        /// <param name="mappingAssmebly">The assembly in which the mappings are contained</param>
        internal ModelMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        /// <summary>
        /// Returns a model mapping which equals the type provided
        /// </summary>
        /// <param name="sourceType">The type to match on</param>
        /// <returns>A model mapping if there is a match, else returns null</returns>
        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }
    }
}

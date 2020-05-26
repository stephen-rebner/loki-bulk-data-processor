using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    internal class ModelMappings : AbstractMappings<AbstractModelMapping>, IModelMappingCollection
    {
        public ModelMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }
    }
}

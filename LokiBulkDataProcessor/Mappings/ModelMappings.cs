using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class ModelMappings : AbstractMappings<AbstractModelMapper>, IModelMappingCollection
    {
        public ModelMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapper GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }
    }
}

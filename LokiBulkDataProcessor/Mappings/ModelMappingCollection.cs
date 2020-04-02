using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class ModelMappingCollection : AbstractMappingCollection<AbstractModelMapping>, IModelMappingCollection
    {
        public ModelMappingCollection(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }
    }
}

using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    //internal class ModelMappings : AbstractMappings<AbstractModelMapping>, IModelMappingCollection
    internal class ModelMappings : IModelMappingCollection
    {
        public ModelMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }

        private void AddMappingsIfMappingAssemblyNotNull(Assembly mappingAssembly)
        {
            if (mappingAssembly != null)
            {
                // todo: update to get types inheriting from interface
                var types = mappingAssembly.FindTypesDerivedFrom(typeof(IModelMapping));

                foreach (var mappingType in types)
                {
                    var instance = (T)Activator.CreateInstance(mappingType);
                    _mappings.Add(instance);
                }
            }
        }
    }
}

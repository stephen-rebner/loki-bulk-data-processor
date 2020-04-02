using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class ModelMappingCollection : IModelMappingCollection
    {
        private readonly IList<AbstractModelMapping> _mappings = new List<AbstractModelMapping>();

        public ModelMappingCollection(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapping GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }

        private void AddMappingsIfMappingAssemblyNotNull(Assembly mappingAssmebly)
        {
            if(mappingAssmebly != null)
            {
                var types = mappingAssmebly.FindTypesDerivedFrom(typeof(AbstractModelMapping));

                foreach (var mappingType in types)
                {
                    var instance = (AbstractModelMapping)Activator.CreateInstance(mappingType);
                    _mappings.Add(instance);
                }
            }
        }
    }
}

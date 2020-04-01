using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class MappingCollection : IMappingCollection
    {
        private readonly IList<AbstractModelMapper> _mappings = new List<AbstractModelMapper>();

        public MappingCollection(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractModelMapper GetMappingFor(Type sourceType)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceType == sourceType);
        }

        private void AddMappingsIfMappingAssemblyNotNull(Assembly mappingAssmebly)
        {
            if(mappingAssmebly != null)
            {
                var types = mappingAssmebly.FindTypesDerivedFrom(typeof(AbstractModelMapper));

                foreach (var mappingType in types)
                {
                    var instance = (AbstractModelMapper)Activator.CreateInstance(mappingType);
                    _mappings.Add(instance);
                }
            }
        }
    }
}

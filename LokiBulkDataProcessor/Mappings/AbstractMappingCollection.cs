using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class AbstractMappingCollection<T> where T : AbstractMapping
    {
        protected readonly IList<T> _mappings = new List<T>();

        protected void AddMappingsIfMappingAssemblyNotNull(Assembly mappingAssmebly)
        {
            if (mappingAssmebly != null)
            {
                var types = mappingAssmebly.FindTypesDerivedFrom(typeof(T));

                foreach (var mappingType in types)
                {
                    var instance = (T)Activator.CreateInstance(mappingType);
                    _mappings.Add(instance);
                }
            }
        }
    }
}

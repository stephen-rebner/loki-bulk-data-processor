using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    public abstract class AbstractMappings<T> where T : AbstractMapping
    {
        protected readonly IList<T> _mappings = new List<T>();

        protected void AddMappingsIfMappingAssemblyNotNull(Assembly mappingAssembly)
        {
            try
            {
                AddMappingsForAssembly(mappingAssembly);
            }
            catch(Exception e)
            {
                throw new InvalidOperationException($"There was an issue instantiating the the mappings. {e.InnerException?.Message}");
            }
        }

        private void AddMappingsForAssembly(Assembly mappingAssembly)
        {
            if (mappingAssembly != null)
            {
                var types = mappingAssembly.FindTypesDerivedFrom(typeof(T));

                foreach (var mappingType in types)
                {
                    var instance = (T)Activator.CreateInstance(mappingType);
                    _mappings.Add(instance);
                }
            }
        }
    }
}

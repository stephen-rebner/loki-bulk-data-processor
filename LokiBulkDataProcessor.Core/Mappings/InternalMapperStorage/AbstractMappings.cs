using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    internal abstract class AbstractMappings<T> where T : AbstractMapping
    {
        protected readonly IList<T> _mappings = new List<T>();

        protected static IEnumerable<Type> FindMappingTypes(IEnumerable<Assembly> mappingAssemblies) =>
            mappingAssemblies
                .Where(assembly => assembly != null)
                .Distinct()
                .SelectMany(assembly => assembly.FindTypesDerivedFrom(typeof(T)));

        protected void AddMappings(IEnumerable<Type> mappingTypes)
        {
            foreach (var mappingType in mappingTypes)
            {
                _mappings.Add(CreateMapping(mappingType));
            }
        }

        protected void ThrowIfDuplicateKeys<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer, string keyDescription)
        {
            var duplicate = _mappings.GroupBy(keySelector, comparer).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                var mappingNames = string.Join(", ", duplicate.Select(mapping => mapping.GetType().FullName));
                throw new InvalidOperationException(
                    $"More than one mapping is registered for {keyDescription} '{duplicate.Key}': {mappingNames}. Remove the duplicate.");
            }
        }

        private static T CreateMapping(Type mappingType)
        {
            try
            {
                return (T)Activator.CreateInstance(mappingType);
            }
            catch (Exception e)
            {
                var cause = e is TargetInvocationException { InnerException: not null } ? e.InnerException : e;
                throw new InvalidOperationException($"There was an issue instantiating the the mappings. {cause.Message}", cause);
            }
        }
    }
}

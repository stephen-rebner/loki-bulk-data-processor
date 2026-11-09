using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Utils.Reflection
{
    internal static class ReflectionUtils
    {
        internal static string[] GetPublicPropertyNames(this Type type)
        {
            var publicProperties = type.GetProperties()
                // Get-only and set-only properties have no accessor of the other kind: skip them (#19).
                .Where(propInfo => propInfo.GetGetMethod(true)?.IsPublic == true)
                .Where(propInfo => propInfo.GetSetMethod(true)?.IsPublic == true)
                .Select(propInfo => propInfo.Name)
                .ToArray();

            publicProperties.ThrowIfCollectionIsNullOrEmpty(
                "The data model you passed contains no public properties",
                nameof(publicProperties));

            return publicProperties;
        }

        internal static IEnumerable<Type> FindTypesDerivedFrom(this Assembly assembly, Type baseType)
        {
            return assembly
                .GetTypes()
                .Where(t => t.IsClass && t.IsSubclassOf(baseType) && !t.IsAbstract);
        }
    }
}

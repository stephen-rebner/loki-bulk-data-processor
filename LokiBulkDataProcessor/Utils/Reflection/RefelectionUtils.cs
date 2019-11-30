using System;
using System.Linq;
using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Utils.Reflection
{
    internal static class ReflectionUtils
    {
        internal static string[] GetPublicPropertyNames(this Type type)
        {
            var publicProperties = type.GetProperties()
                .Where(propInfo => propInfo.GetGetMethod(true).IsPublic)
                .Where(propInfo => propInfo.GetSetMethod(true).IsPublic)
                .Select(propInfo => propInfo.Name)
                .ToArray();

            publicProperties.ThrowIfCollectionIsEmpty(
                "The data model you passed contains no public properties",
                nameof(publicProperties));

            return publicProperties;
        }
    }
}

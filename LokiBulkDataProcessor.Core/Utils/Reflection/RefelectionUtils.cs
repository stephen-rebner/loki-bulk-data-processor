using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Utils.Reflection
{
    internal static class ReflectionUtils
    {
        /// <summary>
        /// Names of the properties Loki copies: public instance, non-indexer, with a public getter and setter.
        /// Get-only and set-only properties are skipped (#19); static properties and indexers are never candidates.
        /// </summary>
        internal static string[] GetPublicPropertyNames(this Type type)
        {
            var publicProperties = type.GetCandidateProperties()
                .Where(IsCopied)
                .Select(propInfo => propInfo.Name)
                .ToArray();

            publicProperties.ThrowIfCollectionIsNullOrEmpty(
                "The data model you passed contains no public properties",
                nameof(publicProperties));

            return publicProperties;
        }

        /// <summary>Names of public instance properties that are skipped because an accessor is missing or not public.</summary>
        internal static string[] GetSkippedPropertyNames(this Type type) =>
            type.GetCandidateProperties()
                .Where(propInfo => !IsCopied(propInfo))
                .Select(propInfo => propInfo.Name)
                .ToArray();

        internal static IEnumerable<Type> FindTypesDerivedFrom(this Assembly assembly, Type baseType)
        {
            return assembly
                .GetTypes()
                .Where(t => t.IsClass && t.IsSubclassOf(baseType) && !t.IsAbstract);
        }

        private static IEnumerable<PropertyInfo> GetCandidateProperties(this Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propInfo => propInfo.GetIndexParameters().Length == 0);

        private static bool IsCopied(PropertyInfo propInfo) =>
            propInfo.GetGetMethod(true)?.IsPublic == true &&
            propInfo.GetSetMethod(true)?.IsPublic == true;
    }
}

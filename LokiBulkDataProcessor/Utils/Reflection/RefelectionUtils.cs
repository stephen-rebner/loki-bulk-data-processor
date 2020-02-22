using System;
using System.Linq;
using System.Reflection;
using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Utils.Reflection
{
    internal static class ReflectionUtils
    {
        /// <summary>
        /// Gets all Property Names which have a public getter and setter
        /// </summary>
        /// <param name="type">The type from which we want to retrieve public Properties</param>
        /// <returns>An array of Property Names</returns>
        internal static string[] GetPublicPropertyNames(this Type type)
        {
            var propertyNames = GetPublicPropertiesForType(type)
                .Select(propInfo => propInfo.Name)
                .ToArray();

            return propertyNames;
        }

        /// <summary>
        /// Gets all Properties which have a public getter and setter
        /// </summary>
        /// <param name="type">The type from which we want to retrieve public Properties</param>
        /// <returns>An array of PropertyInfo objects</returns>
        internal static PropertyInfo[] GetPublicProperties(this Type type)
        {
            return GetPublicPropertiesForType(type);
        }

        /// <summary>
        /// Private Helper method to get Properties which have a 
        /// getter and a setter
        /// </summary>
        /// <param name="type">The type from which we want to retrieve public Properties</param>
        /// <returns>An array of PropertyInfo objects</returns>
        private static PropertyInfo[] GetPublicPropertiesForType(Type type)
        {
            var publicProperties = type.GetProperties()
                .Where(propInfo => propInfo.GetGetMethod(true).IsPublic)
                .Where(propInfo => propInfo.GetSetMethod(true).IsPublic)
                .ToArray();

            publicProperties.ThrowIfCollectionIsNullOrEmpty(
                "The data model you passed contains no public properties",
                nameof(publicProperties));

            return publicProperties;
        }
    }
}

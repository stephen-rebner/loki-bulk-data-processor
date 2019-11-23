using System;
using System.Linq;

namespace Loki.BulkDataProcessor.Utils.Reflection
{
    public static class ReflectionUtils
    {
        public static string[] GetPublicPropertyNamesAsArray(this Type type)
        {
            return type.GetProperties()
                .Select(propInfo => propInfo.Name)
                .ToArray();
        }
    }
}

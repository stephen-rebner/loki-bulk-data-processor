using System;
using System.Collections.Generic;
using System.Linq;

namespace Loki.BulkDataProcessor.Utils.Validation
{
    internal static class ArgumentValidatorExtensions
    {
        internal static void ThrowIfCollectionIsNullOrEmpty<T>(this IEnumerable<T> collection, string errorMessage, string paramName)
        {
            if (!collection.Any()) throw new ArgumentException(errorMessage, paramName);
        }

        internal static void ThrowIfCollectionIsNullOrEmpty<T>(this IEnumerable<T> collection, string paramName)
        {
            if(collection == null || !collection.Any())
            {
                throw new ArgumentException($"The {paramName} collection must not be null or empty.", paramName);
            }
        }

        internal static void ThrowIfNullOrEmptyString(this string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value)) 
            {
                throw new ArgumentException($"{paramName} must not be null or empty.", paramName);
            }
        }

        internal static void ThrowIfLessThanZero(this int value, string paramName)
        {
            if (value < 0) throw new ArgumentException($"The {paramName} value must be greater than or equal to 0", paramName);
        }
    }
}
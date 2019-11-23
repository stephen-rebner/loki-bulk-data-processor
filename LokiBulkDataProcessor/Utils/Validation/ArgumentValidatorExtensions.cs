using System;
using System.Collections.Generic;
using System.Linq;

namespace Loki.BulkDataProcessor.Utils.Validation
{
    internal static class ArgumentValidatorExtensions
    {
        internal static void ThrowExceptionIfArgumentIsNull<T>(this T argument, string errorMessage, string paramName)
        {
            if (argument == null) throw new ArgumentNullException(paramName, errorMessage);
        }

        internal static void ThrowExceptionIfCollectionIsEmpty<T>(this IEnumerable<T> collection, string errorMessage, string paramName)
        {
            if (!collection.Any()) throw new ArgumentException(errorMessage, paramName);
        }

        internal static void ThrowExceptionIfNullOrEmptyString(this string value, string errorMessage, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(errorMessage, paramName);
        }

        internal static void ThrowExecptionIfGreaterThanOrEqualToZero(this int value, string paramName)
        {
            if (value >= 0) throw new ArgumentException($"The {paramName} value must be greater than or equal to 0", paramName);
        }

        internal static void ThrowExecptionIfLessThanZero(this int value, string paramName)
        {
            if (value < 0) throw new ArgumentException($"The {paramName} value must be greater than or equal to 0", paramName);
        }
    }
}
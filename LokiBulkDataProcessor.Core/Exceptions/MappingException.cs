using System;

namespace Loki.BulkDataProcessor.Exceptions
{
    internal class MappingException : Exception
    {
        internal MappingException(string errorMessage) : base(errorMessage) { }
    }
}

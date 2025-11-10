using System;

namespace Loki.BulkDataProcessor.Core.Exceptions
{
    public class MappingException : Exception
    {
        public MappingException(string errorMessage) : base(errorMessage) { }
    }
}

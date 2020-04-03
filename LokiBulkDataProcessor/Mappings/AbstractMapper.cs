using Loki.BulkDataProcessor.Exceptions;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Mappings
{
    public class AbstractMapper
    {
        internal Dictionary<string, string> ColumnMappings { get; }

        protected AbstractMapper()
        {
            ColumnMappings = new Dictionary<string, string>();
        }

        protected void ThrowIfDuplicateSourceColumn(string sourceColumn)
        {
            if (ColumnMappings.ContainsKey(sourceColumn))
            {
                throw new MappingException($"The mapping contains a duplicate source column: {sourceColumn}");
            }
        }
    }
}

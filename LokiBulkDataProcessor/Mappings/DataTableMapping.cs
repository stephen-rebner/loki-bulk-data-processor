using Loki.BulkDataProcessor.Exceptions;
using System.Diagnostics;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class DataTableMapping : AbstractMapper
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _currentColumnName;
        public abstract string SourceTableName { get; }

        public DataTableMapping Map(string columnName)
        {
            _currentColumnName = columnName;

            ThrowIfDuplicateSourceColumn(_currentColumnName);

            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            if(string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {SourceTableName} data table contains a null or empty destination column.");
            }

            if(ColumnMappings.ContainsValue(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {SourceTableName} data table contains duplicate destination columns.");
            }

            ColumnMappings.Add(_currentColumnName, destinationColumnName);
        }
    }
}

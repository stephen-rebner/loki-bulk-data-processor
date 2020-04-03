using Loki.BulkDataProcessor.Exceptions;
using System;
using System.Diagnostics;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class AbstractDataTableMapper : AbstractMapper
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _currentColumnName;
        protected string _tableName;

        internal string TableName => _tableName;

        public AbstractDataTableMapper ForDataTable(string tableName)
        {
            if(_tableName != null) throw new InvalidOperationException($"The '{tableName}' table has already been defined for this mapping");

            _tableName = tableName;

            return this;
        }

        public AbstractDataTableMapper Map(string columnName)
        {
            _currentColumnName = columnName;

            ThrowIfDuplicateSourceColumn(_currentColumnName);

            return this;
        }

        public AbstractDataTableMapper ToDestinationColumn(string destinationColumnName)
        {
            if(string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {_tableName} data table contains a null or empty destination column.");
            }

            if(ColumnMappings.ContainsValue(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {_tableName} data table contains duplicate destination columns.");
            }

            ColumnMappings.Add(_currentColumnName, destinationColumnName);
            return this;
        }
    }
}

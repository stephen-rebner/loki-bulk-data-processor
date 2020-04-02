using System;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class AbstractDataTableMapping : AbstractMapping
    {
        private string _propertyName;
        protected string _tableName;

        internal string TableName => _tableName;

        public AbstractDataTableMapping ForTable(string tableName)
        {
            if(_tableName == null) throw new InvalidOperationException("The table has already been defined for this mapping");

            _tableName = tableName;

            return this;
        }

        public AbstractDataTableMapping Map<TKey>(string columnName)
        {
            _propertyName = columnName;
            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            ColumnMappings.Add(_propertyName, destinationColumnName);
        }
    }
}

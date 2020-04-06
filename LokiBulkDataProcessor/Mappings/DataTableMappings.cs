using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class DataTableMappings : AbstractMappings<AbstractDataTableMapper>, IDataTableMappingCollection
    {
        public DataTableMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractDataTableMapper GetMappingFor(string tableName)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceTableName.Equals(tableName, StringComparison.Ordinal));
        }
    }
}

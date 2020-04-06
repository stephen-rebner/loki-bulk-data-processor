using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    internal class DataTableMappings : AbstractMappings<DataTableMapping>, IDataTableMappingCollection
    {
        public DataTableMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public DataTableMapping GetMappingFor(string tableName)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceTableName.Equals(tableName, StringComparison.Ordinal));
        }
    }
}

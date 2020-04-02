using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class DataTableMappingCollection : AbstractMappingCollection<AbstractDataTableMapping>, IDataTableMappingCollection
    {
        public DataTableMappingCollection(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        public AbstractDataTableMapping GetMappingFor(string tableName)
        {
            return _mappings.FirstOrDefault(mapping => mapping.TableName.Equals(tableName, StringComparison.Ordinal));
        }
    }
}

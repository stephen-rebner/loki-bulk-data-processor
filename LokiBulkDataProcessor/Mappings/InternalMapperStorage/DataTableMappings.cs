using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Loki.BulkDataProcessor.Mappings.InternalMapperStorage
{
    /// <summary>
    /// A class that holds a collection of Data Table mappings
    /// The mappings are added dynamically when instantiated
    /// </summary>
    internal class DataTableMappings : AbstractMappings<DataTableMapping>, IDataTableMappingCollection
    {
        /// <summary>
        /// Adds all data table mappings contained in the assembly provided
        /// </summary>
        /// <param name="mappingAssmebly">The assembly in which the mappings are contained</param>
        public DataTableMappings(Assembly mappingAssmebly)
        {
            AddMappingsIfMappingAssemblyNotNull(mappingAssmebly);
        }

        /// <summary>
        ///  Returns a model mapping which equals the table name provided
        /// </summary>
        /// <param name="tableName">The name of the data table to match on</param>
        /// <returns>A data table mapping if there is a match, else returns null</returns>
        public DataTableMapping GetMappingFor(string tableName)
        {
            return _mappings.FirstOrDefault(mapping => mapping.SourceTableName.Equals(tableName, StringComparison.Ordinal));
        }
    }
}

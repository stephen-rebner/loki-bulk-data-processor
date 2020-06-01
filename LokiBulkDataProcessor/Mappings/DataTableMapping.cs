using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class DataTableMapping : AbstractMapping, IMapDataTableSource
    {
        public abstract string SourceTableName { get; }

        public DataTableMapping()
        {
            MappingInfo = new DataTableMappingInfo(SourceTableName);
        }

        /// <summary>
        /// Determines the data table column to map as the source
        /// </summary>
        /// <param name="sourceColumn"></param>
        /// <returns></returns>
        public IToDestination Map(string sourceColumn)
        {
            var mappingInfo = (DataTableMappingInfo)MappingInfo;

            mappingInfo.Map(sourceColumn);

            return mappingInfo;
        }
    }
}

using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class DataMapping : AbstractMapping, IMapDataTableSource
    {
        public abstract string SourceTableName { get; }

        public DataMapping()
        {
            MappingInfo = new DataMappingInfo(SourceTableName);
        }

        /// <summary>
        /// Determines the data column to map as the source
        /// </summary>
        /// <param name="sourceColumn"></param>
        /// <returns></returns>
        public IToDestination Map(string sourceColumn)
        {
            var mappingInfo = (DataMappingInfo)MappingInfo;

            mappingInfo.Map(sourceColumn);

            return mappingInfo;
        }
    }
}

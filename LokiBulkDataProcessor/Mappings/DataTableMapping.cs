using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class DataTableMapping : IMapDataTableSource
    {
        internal DataTableMappingInfo MappingInfo;

        public abstract string SourceTableName { get; }

        public DataTableMapping()
        {
            MappingInfo = new DataTableMappingInfo(SourceTableName);
        }

        public IToDestination Map(string sourceColumn)
        {
            MappingInfo.Map(sourceColumn);

            return MappingInfo;
        }
    }
}

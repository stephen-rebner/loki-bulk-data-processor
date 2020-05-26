using Loki.BulkDataProcessor.Mappings.Interfaces;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal class DataTableMappingInfo : AbstractMappingInfo, IMapDataTableSource
    {
        public IToDestination Map(string sourceColumnName)
        {
            _currentMappingMetaData = new MappingMetaData { SourceColumn = sourceColumnName };

            MappingMetaDataCollection.Add(_currentMappingMetaData);

            return this;
        }
    }
}

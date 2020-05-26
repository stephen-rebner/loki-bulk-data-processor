using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal abstract class AbstractMappingInfo : IToDestination, IAsPrimaryKey
    {
        protected MappingMetaData _currentMappingMetaData;

        internal IList<MappingMetaData> MappingMetaDataCollection { get; set; }

        public void AsPrimaryKey()
        {
            _currentMappingMetaData.IsPrimaryKey = true;

            MappingMetaDataCollection[MappingMetaDataCollection.Count -1] = _currentMappingMetaData;
        }

        public IAsPrimaryKey ToDestinationColumn(string destinationColumnName)
        {
            _currentMappingMetaData.DestinationColumn = destinationColumnName;

            UpdateMappingCollection(_currentMappingMetaData);

            return this;
        }

        private void UpdateMappingCollection(MappingMetaData mappingMetaData)
        {
            MappingMetaDataCollection[MappingMetaDataCollection.Count - 1] = mappingMetaData;

        }
    }
}

using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal abstract class AbstractMappingInfo : IAsPrimaryKey
    {
        protected MappingMetaData _currentMappingMetaData;

        internal IList<MappingMetaData> MappingMetaDataCollection { get; set; }

        internal AbstractMappingInfo()
        {
            MappingMetaDataCollection = new List<MappingMetaData>();
        }

        public void AsPrimaryKey()
        {
            _currentMappingMetaData.IsPrimaryKey = true;

            UpdateMappingCollection(_currentMappingMetaData);
        }

        private void UpdateMappingCollection(MappingMetaData mappingMetaData)
        {
            MappingMetaDataCollection[MappingMetaDataCollection.Count - 1] = mappingMetaData;
        }
    }
}

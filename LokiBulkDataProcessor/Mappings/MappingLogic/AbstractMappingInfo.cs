using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal abstract class AbstractMappingInfo : IAsPrimaryKey
    {
        protected MappingMetaData _currentMappingMetaData;

        internal IList<MappingMetaData> MappingMetaDataCollection { get; set; }

        public void AsPrimaryKey()
        {
            _currentMappingMetaData.IsPrimaryKey = true;

            MappingMetaDataCollection[MappingMetaDataCollection.Count -1] = _currentMappingMetaData;
        }

        protected void ThrowIfDuplicateSourceColumn(string sourceColumn)
        {
            //if (ColumnMappings.ContainsKey(sourceColumn))
            //{
            //    throw new MappingException($"The mapping contains a duplicate source column: {sourceColumn}");
            //}
        }

        private void UpdateMappingCollection(MappingMetaData mappingMetaData)
        {
            MappingMetaDataCollection[MappingMetaDataCollection.Count - 1] = mappingMetaData;

        }
    }
}

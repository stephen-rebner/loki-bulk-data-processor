using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal abstract class AbstractMappingInfo : IAsPrimaryKey
    {
        protected MappingMetaData _currentMappingMetaData;

        /// <summary>
        /// A collection of meta data containing mapping information for each object
        /// property / in-memory datatable column
        /// </summary>
        internal IList<MappingMetaData> MappingMetaDataCollection { get; set; }

        internal AbstractMappingInfo()
        {
            MappingMetaDataCollection = new List<MappingMetaData>();
        }

        /// <summary>
        /// Maps the object property / in-memory datatable column as the primary key
        /// </summary>
        public void AsPrimaryKey()
        {
            ThrowIfDuplicatePrimaryKey();

            _currentMappingMetaData.IsPrimaryKey = true;

            UpdateMappingCollection(_currentMappingMetaData);
        }

        private void ThrowIfDuplicatePrimaryKey()
        {
            if(MappingMetaDataCollection.Any(metaData => metaData.IsPrimaryKey))
            {
                throw new MappingException("Composite primary keys are currently not supported");
            }
        }

        private void UpdateMappingCollection(MappingMetaData mappingMetaData)
        {
            MappingMetaDataCollection[MappingMetaDataCollection.Count - 1] = mappingMetaData;
        }
    }
}

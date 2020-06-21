using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal class DataTableMappingInfo : AbstractMappingInfo, IMapDataTableSource, IToDestination
    {
        private readonly string _sourceTableName;

        public DataTableMappingInfo(string sourceTableName)
        {
            _sourceTableName = sourceTableName;
        }

        public IToDestination Map(string sourceColumnName)
        {
            ThrowIfDuplicateSourceColumn(sourceColumnName);

            _currentMappingMetaData = new MappingMetaData { SourceColumn = sourceColumnName };

            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            ThrowIfDestinationColumnIsNullOrWhiteSpace(destinationColumnName);

            ThrowIfDuplicateDestinationColumn(destinationColumnName);

            _currentMappingMetaData.DestinationColumn = destinationColumnName;

            MappingMetaDataCollection.Add(_currentMappingMetaData);
        }

        private void ThrowIfDuplicateSourceColumn(string sourceColumn)
        {
            if (MappingMetaDataCollection.Any(metaData => metaData.SourceColumn.Equals(sourceColumn, StringComparison.Ordinal)))
            {
                throw new MappingException($"The mapping for the {_sourceTableName} data table contains a duplicate source column: {sourceColumn}");
            }
        }

        private void ThrowIfDestinationColumnIsNullOrWhiteSpace(string destinationColumnName)
        {
            if (string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {_sourceTableName} data table contains a null or empty destination column.");
            }
        }

        private void ThrowIfDuplicateDestinationColumn(string destinationColumnName)
        {
            if (MappingMetaDataCollection.Any(metaData => metaData.DestinationColumn.Equals(destinationColumnName, StringComparison.Ordinal)))
            {
                throw new MappingException($"The mapping for the {_sourceTableName} data table contains duplicate destination columns.");
            }
        }

    }
}

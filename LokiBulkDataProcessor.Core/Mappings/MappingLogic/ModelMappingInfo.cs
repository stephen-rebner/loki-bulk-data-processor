using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal class ModelMappingInfo<TSource> : AbstractMappingInfo, IToDestination where TSource : class
    {
        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var member = keySelector.Body as MemberExpression;

            var propertyName = member.Member.Name;

            ThrowIfDuplicateSourceColumn(propertyName);

            _currentMappingMetaData = new MappingMetaData { SourceColumn = propertyName };

            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            ThrowIfDestinationColumnIsNullOrWhiteSpace(destinationColumnName);

            ThrowIfDuplicateDestinationColumn(destinationColumnName);

            _currentMappingMetaData.DestinationColumn = destinationColumnName;

            MappingMetaDataCollection.Add(_currentMappingMetaData);
        }

        protected void ThrowIfDuplicateSourceColumn(string sourceColumn)
        {
            if (MappingMetaDataCollection.Any(metaData => metaData.SourceColumn.Equals(sourceColumn, StringComparison.Ordinal)))
            {
                throw new MappingException($"The mapping contains a duplicate source column: {sourceColumn}");
            }
        }

        private void ThrowIfDestinationColumnIsNullOrWhiteSpace(string destinationColumnName)
        {
            if (string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {typeof(TSource).Name} model contains a null or empty destination column.");
            }
        }

        private void ThrowIfDuplicateDestinationColumn(string destinationColumnName)
        {
            if (MappingMetaDataCollection.Any(metaData => metaData.DestinationColumn.Equals(destinationColumnName, StringComparison.Ordinal)))
            {
                throw new MappingException($"The mapping for the {typeof(TSource).Name} model contains duplicate destination columns.");
            }
        }
    }
}

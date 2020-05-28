using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
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

            MappingMetaDataCollection.Add(_currentMappingMetaData);

            return this;
        }

        public IAsPrimaryKey ToDestinationColumn(string destinationColumnName)
        {
            ThrowIfDestinationColumnIsNullOrWhiteSpace(destinationColumnName);

            ThrowIfDuplicateDestinationColumn(destinationColumnName);

            _currentMappingMetaData.DestinationColumn = destinationColumnName;

            MappingMetaDataCollection.Add(_currentMappingMetaData);

            return this;
        }

        public void ThrowIfDestinationColumnIsNullOrWhiteSpace(string destinationColumnName)
        {
            if (string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {SourceType.Name} model contains a null or empty destination column.");
            }
        }

        private void ThrowIfDuplicateDestinationColumn(string destinationColumnName)
        {
            //if (ColumnMappings.ContainsValue(destinationColumnName))
            //{
            //    throw new MappingException($"The mapping for the {SourceType.Name} model contains duplicate destination columns.");
            //}
        }
    }
}

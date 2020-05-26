using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings.MappingLogic
{
    internal class ModelMappingInfo : AbstractMappingInfo
    {
        public IToDestination Map<TSource,TKey>(Expression<Func<TSource, TKey>> keySelector) where TSource : class
        {
            var member = keySelector.Body as MemberExpression;

            var propertyName = member.Member.Name;

            _currentMappingMetaData = new MappingMetaData { SourceColumn = propertyName };

            MappingMetaDataCollection.Add(_currentMappingMetaData);

            return this;
        }
    }
}

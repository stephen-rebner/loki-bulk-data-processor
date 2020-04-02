using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class AbstractModelMapping<TSource> : AbstractModelMapping where TSource : class
    {
        private string _propertyName;

        public AbstractModelMapping() : base(typeof(TSource))
        {
        }

        public AbstractModelMapping<TSource> Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var member = keySelector.Body as MemberExpression;

            _propertyName = member.Member.Name;
            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            ColumnMappings.Add(_propertyName, destinationColumnName);
        }
    }

    public class AbstractModelMapping : AbstractMapping
    {
        internal Type SourceType { get; }

        public AbstractModelMapping(Type sourceType)
        {
            SourceType = sourceType;
        }
    }
}

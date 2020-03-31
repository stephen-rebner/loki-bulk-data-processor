using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class AbstractModelMapping<TSource> : AbstractModelMapper where TSource : class
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

        public void ValidateTheMappings()
        {
            var test = ColumnMappings;
        }
    }

    public class AbstractModelMapper
    {
        internal Dictionary<string, string> ColumnMappings { get; }
        internal Type SourceType { get; }

        public AbstractModelMapper(Type sourceType)
        {
            ColumnMappings = new Dictionary<string, string>();
            SourceType = sourceType;
        }
    }
}

using Loki.BulkDataProcessor.Exceptions;
using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class ModelMapping<TSource> : AbstractModelMapper where TSource : class
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _currentPropertyName;

        public ModelMapping() : base(typeof(TSource))
        {
        }

        public ModelMapping<TSource> Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var member = keySelector.Body as MemberExpression;

            _currentPropertyName = member.Member.Name;

            ThrowIfDuplicateSourceColumn(_currentPropertyName);

            return this;
        }

        public void ToDestinationColumn(string destinationColumnName)
        {
            if(string.IsNullOrWhiteSpace(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {SourceType.Name} model contains a null or empty destination column.");
            }

            if (ColumnMappings.ContainsValue(destinationColumnName))
            {
                throw new MappingException($"The mapping for the {SourceType.Name} model contains duplicate destination columns.");
            }

            ColumnMappings.Add(_currentPropertyName, destinationColumnName);
        }
    }

    public class AbstractModelMapper : AbstractMapper
    {
        internal Type SourceType { get; }

        public AbstractModelMapper(Type sourceType)
        {
            SourceType = sourceType;
        }
    }
}

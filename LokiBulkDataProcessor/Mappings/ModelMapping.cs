using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;
using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class ModelMapping<TSource> : AbstractModelMapping, IMapModelSource<TSource> where TSource : class
    {
        public ModelMapping() : base(typeof(TSource))
        {
        }

        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var mappingInfo = (ModelMappingInfo)MappingInfo;

            mappingInfo.Map(keySelector);

            return mappingInfo;
        }

        //public void ToDestinationColumn(string destinationColumnName)
        //{
        //    if(string.IsNullOrWhiteSpace(destinationColumnName))
        //    {
        //        throw new MappingException($"The mapping for the {SourceType.Name} model contains a null or empty destination column.");
        //    }

        //    if (ColumnMappings.ContainsValue(destinationColumnName))
        //    {
        //        throw new MappingException($"The mapping for the {SourceType.Name} model contains duplicate destination columns.");
        //    }

        //    ColumnMappings.Add(_currentPropertyName, destinationColumnName);
        //}
    }

    public class AbstractModelMapping : AbstractMapping
    {
        internal Type SourceType { get; }

        public AbstractModelMapping(Type sourceType) : base(new ModelMappingInfo())
        {
            SourceType = sourceType;
        }
    }
}

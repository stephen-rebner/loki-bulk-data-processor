using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;
using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class ModelMapping<TSource> : IMapModelSource<TSource> where TSource : class
    {
        internal ModelMappingInfo<TSource> MappingInfo { get; }

        public ModelMapping()
        {
            MappingInfo = new ModelMappingInfo<TSource>();
        }

        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            MappingInfo.Map(keySelector);

            return MappingInfo;
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
}

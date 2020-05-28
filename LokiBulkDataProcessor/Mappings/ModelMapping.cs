using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;
using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class ModelMapping<TSource> : IModelMapping, IMapModelSource<TSource> where TSource : class
    {
        internal ModelMappingInfo<TSource> MappingInfo { get; }

        public Type SourceType => typeof(TSource);

        public ModelMapping()
        {
            MappingInfo = new ModelMappingInfo<TSource>();
        }

        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            MappingInfo.Map(keySelector);

            return MappingInfo;
        }
    }
}

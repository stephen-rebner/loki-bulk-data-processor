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
            MappingInfo = new ModelMappingInfo<TSource>();
        }

        /// <summary>
        /// Determines the property on the model object to map as the source
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var mappingInfo = (ModelMappingInfo<TSource>)MappingInfo;

            mappingInfo.Map(keySelector);

            return mappingInfo;
        }
    }

    public abstract class AbstractModelMapping : AbstractMapping
    {
        internal Type SourceType { get; }

        public AbstractModelMapping(Type sourceType)
        {
            SourceType = sourceType;
        }
    }
}

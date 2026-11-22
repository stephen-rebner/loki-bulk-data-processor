using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;
using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings
{
    /// <summary>
    /// Maps the public properties of <typeparamref name="TSource"/> to destination columns for
    /// <c>IBulkProcessor.SaveAsync&lt;T&gt;(IEnumerable&lt;T&gt;, ...)</c>. Subclass it, call <see cref="Map{TKey}"/> in the
    /// constructor, and register the assembly with <c>LokiBulkDataProcessorOptions.AddMappingsFrom</c>. Mappings are
    /// instantiated and validated at registration; two mappings for the same model type fail at startup.
    /// </summary>
    /// <remarks>Only the mapped properties are copied. Without a mapping, every public read/write property is copied to the
    /// column of the same name.</remarks>
    /// <typeparam name="TSource">The model type being saved.</typeparam>
    public abstract class ModelMapping<TSource> : AbstractModelMapping, IMapModelSource<TSource> where TSource : class
    {
        /// <summary>Creates the mapping; call <see cref="Map{TKey}"/> from the derived constructor.</summary>
        public ModelMapping() : base(typeof(TSource))
        { 
            MappingInfo = new ModelMappingInfo<TSource>();
        }

        /// <summary>
        /// Determines the property on the model object to map as the source; follow it with
        /// <see cref="IToDestination.ToDestinationColumn"/>.
        /// </summary>
        /// <typeparam name="TKey">The property type.</typeparam>
        /// <param name="keySelector">A public property with a public getter, for example <c>post =&gt; post.Title</c>.</param>
        /// <returns>The step that names the destination column.</returns>
        public IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector)
        {
            var mappingInfo = (ModelMappingInfo<TSource>)MappingInfo;

            mappingInfo.Map(keySelector);

            return mappingInfo;
        }
    }

    /// <summary>Non-generic base of <see cref="ModelMapping{TSource}"/>. Derive from <see cref="ModelMapping{TSource}"/>, not from this class.</summary>
    public abstract class AbstractModelMapping : AbstractMapping
    {
        internal Type SourceType { get; }

        /// <summary>Creates the mapping for <paramref name="sourceType"/>.</summary>
        /// <param name="sourceType">The model type being mapped.</param>
        public AbstractModelMapping(Type sourceType)
        {
            SourceType = sourceType;
        }
    }
}

using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    /// <summary>A mapping for a model type.</summary>
    public interface IModelMapping
    {
        /// <summary>The model type the mapping applies to.</summary>
        public Type SourceType { get; }
    }
}

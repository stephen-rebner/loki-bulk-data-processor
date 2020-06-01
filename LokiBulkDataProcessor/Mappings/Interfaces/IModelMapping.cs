using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IModelMapping
    {
        public Type SourceType { get; }
    }
}

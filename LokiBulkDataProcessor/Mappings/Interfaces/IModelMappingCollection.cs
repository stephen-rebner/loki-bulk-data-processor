using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IModelMappingCollection
    {
        AbstractModelMapper GetMappingFor(Type sourceType);
    }
}

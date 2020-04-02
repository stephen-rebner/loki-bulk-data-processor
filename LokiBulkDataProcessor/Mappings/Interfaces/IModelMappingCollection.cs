using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IModelMappingCollection
    {
        AbstractModelMapping GetMappingFor(Type sourceType);
    }
}

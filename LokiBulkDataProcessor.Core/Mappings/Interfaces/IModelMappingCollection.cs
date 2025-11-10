using System;

namespace Loki.BulkDataProcessor.Core.Mappings.Interfaces
{
    public interface IModelMappingCollection
    {
        AbstractModelMapping GetMappingFor(Type sourceType);
    }
}

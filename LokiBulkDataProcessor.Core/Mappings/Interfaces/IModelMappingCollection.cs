using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    internal interface IModelMappingCollection
    {
        AbstractModelMapping GetMappingFor(Type sourceType);
    }
}

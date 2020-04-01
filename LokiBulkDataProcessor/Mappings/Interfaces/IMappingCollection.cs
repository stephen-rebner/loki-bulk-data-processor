using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IMappingCollection
    {
        AbstractModelMapper GetMappingFor(Type sourceType);
    }
}

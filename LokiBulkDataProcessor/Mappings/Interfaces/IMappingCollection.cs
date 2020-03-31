using System;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    internal interface IMappingCollection
    {
        AbstractModelMapper GetMappingFor(Type sourceType);
    }
}

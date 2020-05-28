using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public class AbstractMapping
    {
        internal AbstractMappingInfo MappingInfo { get; }

        internal AbstractMapping(AbstractMappingInfo mappingInfo)
        {
            MappingInfo = mappingInfo;
        }
    }
}

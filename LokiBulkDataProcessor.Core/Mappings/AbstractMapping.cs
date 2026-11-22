using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    /// <summary>Common base of <see cref="ModelMapping{TSource}"/> and <see cref="DataMapping"/>. Derive from one of those, not from this class.</summary>
    public class AbstractMapping
    {
        internal AbstractMappingInfo MappingInfo { get; set; }
    }
}

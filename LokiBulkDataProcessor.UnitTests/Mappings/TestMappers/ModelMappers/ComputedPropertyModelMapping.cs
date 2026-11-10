using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    /// <summary>Maps a get-only property, which Loki never copies (#19).</summary>
    public class ComputedPropertyModelMapping : ModelMapping<ModelWithComputedProperty>
    {
        public ComputedPropertyModelMapping()
        {
            Map(o => o.Id).ToDestinationColumn("id");
            Map(o => o.Reference).ToDestinationColumn("reference");
        }
    }
}

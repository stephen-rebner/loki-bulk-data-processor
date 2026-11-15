using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    /// <summary>A valid second mapping for the model ValidModelMapping1 already maps: a duplicate.</summary>
    public class SecondValidModelObjectMapping : ModelMapping<ValidModelObject>
    {
        public SecondValidModelObjectMapping()
        {
            Map(o => o.PublicInt).ToDestinationColumn("other_int");
        }
    }
}

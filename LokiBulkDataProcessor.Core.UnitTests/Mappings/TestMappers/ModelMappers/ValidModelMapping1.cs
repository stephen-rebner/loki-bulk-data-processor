using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.Core.UnitTests.TestModels;

namespace LokiBulkDataProcessor.Core.UnitTests.Mappings.TestMappers
{
    public class ValidModelMapping1 : ModelMapping<ValidModelObject>
    {
        public ValidModelMapping1()
        {
            Map(o => o.PublicInt).ToDestinationColumn("public_int");
            Map(o => o.PublicBool).ToDestinationColumn("public_bool");
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.PublicString).ToDestinationColumn("public_string");
        }
    }

}

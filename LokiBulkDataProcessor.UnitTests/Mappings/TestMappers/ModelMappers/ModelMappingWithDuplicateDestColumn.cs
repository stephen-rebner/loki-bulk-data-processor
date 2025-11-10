using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ModelMappingWithDuplicateDestColumn : ModelMapping<ValidModelObject>
    {
        public ModelMappingWithDuplicateDestColumn()
        {
            Map(o => o.PublicInt).ToDestinationColumn("public_int");
            Map(o => o.PublicBool).ToDestinationColumn("public_bool");
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.PublicString).ToDestinationColumn("base_int");
        }
    }
}

using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ModelMappingWithDuplicatePrimaryKey : ModelMapping<ValidModelObject>
    {
        public ModelMappingWithDuplicatePrimaryKey()
        {
            Map(o => o.PublicInt).ToDestinationColumn("public_int").AsPrimaryKey();
            Map(o => o.PublicBool).ToDestinationColumn("public_bool").AsPrimaryKey();
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.PublicString).ToDestinationColumn("base_int");
        }
    }
}

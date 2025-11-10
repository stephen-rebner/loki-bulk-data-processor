using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ModelMappingWithDuplicateSourceColumn : ModelMapping<ValidModelObject>
    {
        public ModelMappingWithDuplicateSourceColumn()
        {
            Map(o => o.PublicInt).ToDestinationColumn("public_int");
            Map(o => o.PublicBool).ToDestinationColumn("public_bool");
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.PublicString).ToDestinationColumn("public_string");
            Map(o => o.PublicString).ToDestinationColumn("test");
        }
    }
}

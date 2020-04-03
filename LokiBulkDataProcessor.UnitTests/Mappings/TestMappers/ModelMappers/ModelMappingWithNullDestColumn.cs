using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ModelMappingWithNullDestColumn : AbstractModelMapper<ValidModelObject>
    {
        public ModelMappingWithNullDestColumn()
        {
            Map(o => o.PublicInt).ToDestinationColumn("public_int");
            Map(o => o.PublicBool).ToDestinationColumn("public_bool");
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.PublicString).ToDestinationColumn(null);
        }
    }
}

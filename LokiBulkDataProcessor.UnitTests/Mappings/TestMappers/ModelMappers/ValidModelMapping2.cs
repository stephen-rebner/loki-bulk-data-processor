using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ValidModelMapping2 : ModelMapping<ValidModelObjectB>
    {
        public ValidModelMapping2()
        {
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.AnotherColumn).ToDestinationColumn("another_column");
        }
    }
}

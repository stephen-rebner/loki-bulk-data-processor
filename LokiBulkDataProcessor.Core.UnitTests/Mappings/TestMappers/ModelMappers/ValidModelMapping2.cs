using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.Core.UnitTests.TestModels;

namespace LokiBulkDataProcessor.Core.UnitTests.Mappings.TestMappers
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

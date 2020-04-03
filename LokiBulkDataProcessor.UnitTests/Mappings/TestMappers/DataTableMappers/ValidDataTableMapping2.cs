using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ValidDataTableMapping2 : AbstractDataTableMapper
    {
        public ValidDataTableMapping2()
        {
            ForDataTable("TableB")
            .Map("BaseInt").ToDestinationColumn("base_int")
            .Map("AnotherColumn").ToDestinationColumn("another_column");
        }
    }
}

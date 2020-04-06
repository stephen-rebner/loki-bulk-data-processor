using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ValidDataTableMapping2 : DataTableMapping
    {
        public override string SourceTableName => "TableB";

        public ValidDataTableMapping2()
        {
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("AnotherColumn").ToDestinationColumn("another_column");
        }
    }
}

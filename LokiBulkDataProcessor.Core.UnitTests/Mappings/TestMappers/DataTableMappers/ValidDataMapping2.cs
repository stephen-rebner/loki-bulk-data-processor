using Loki.BulkDataProcessor.Core.Mappings;

namespace LokiBulkDataProcessor.Core.UnitTests.Mappings.TestMappers
{
    public class ValidDataMapping2 : DataMapping
    {
        public override string SourceTableName => "TableB";

        public ValidDataMapping2()
        {
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("AnotherColumn").ToDestinationColumn("another_column");
        }
    }
}

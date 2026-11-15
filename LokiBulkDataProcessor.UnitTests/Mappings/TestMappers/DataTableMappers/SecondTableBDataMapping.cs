using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    /// <summary>A valid second mapping for the source name ValidDataMapping2 already uses: a duplicate.</summary>
    public class SecondTableBDataMapping : DataMapping
    {
        public override string SourceTableName => "TableB";

        public SecondTableBDataMapping()
        {
            Map("BaseInt").ToDestinationColumn("other_int");
        }
    }
}

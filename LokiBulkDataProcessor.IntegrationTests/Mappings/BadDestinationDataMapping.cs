using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    /// <summary>Maps to a destination column that does not exist, to test the documented error (#15).</summary>
    public class BadDestinationDataMapping : DataMapping
    {
        public const string SourceName = "BadDestinationPosts";

        public override string SourceTableName => SourceName;

        public BadDestinationDataMapping()
        {
            Map("Title").ToDestinationColumn("NoSuchColumn");
            Map("Content").ToDestinationColumn("Content");
            Map("BlogId").ToDestinationColumn("BlogId");
        }
    }
}

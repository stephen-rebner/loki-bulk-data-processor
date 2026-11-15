using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.Mappings
{
    public class PostDataMapping : DataMapping
    {
        public override string SourceTableName => "Posts";

        public PostDataMapping()
        {
            Map("ATitle").ToDestinationColumn("Title");
            Map("ContentA").ToDestinationColumn("Content");
            Map("ABlogId").ToDestinationColumn("BlogId");
        }
    }
}

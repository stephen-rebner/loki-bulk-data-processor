using Loki.BulkDataProcessor.Core.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
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

using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    public class PostDataTableMapping : AbstractDataTableMapper
    {
        public override string SourceTableName => "Posts";

        public PostDataTableMapping()
        {
            Map("ATitle").ToDestinationColumn("Title");
            Map("ContentA").ToDestinationColumn("Content");
            Map("ABlogId").ToDestinationColumn("BlogId");
        }
    }
}

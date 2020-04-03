using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    public class PostDataTableMapping : AbstractDataTableMapper
    {
        public PostDataTableMapping()
        {
            ForDataTable("Posts")
                .Map("ATitle").ToDestinationColumn("Title")
                .Map("ContentA").ToDestinationColumn("Content")
                .Map("ABlogId").ToDestinationColumn("BlogId");
        }
    }
}

using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    /// <summary>Selected with an explicit sourceName (#16).</summary>
    public class PostImportDataMapping : DataMapping
    {
        public const string SourceName = "PostImport";

        public override string SourceTableName => SourceName;

        public PostImportDataMapping()
        {
            Map("ImportTitle").ToDestinationColumn("Title");
            Map("ImportContent").ToDestinationColumn("Content");
            Map("ImportBlogId").ToDestinationColumn("BlogId");
        }
    }
}

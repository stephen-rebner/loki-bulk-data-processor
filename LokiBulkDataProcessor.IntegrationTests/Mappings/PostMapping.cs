using Loki.BulkDataProcessor.Core.Mappings;
using LokiBulkDataProcessor.IntegrationTests.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    public class PostMapping : ModelMapping<Post>
    {
        public PostMapping()
        {
            Map(post => post.Id).ToDestinationColumn("Id");
            Map(post => post.BlogId).ToDestinationColumn("BlogId");
            Map(post => post.Title).ToDestinationColumn("Title");
            Map(post => post.Content).ToDestinationColumn("Content");
        }
    }
}

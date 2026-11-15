using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.Mappings
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

using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.Mappings
{
    /// <summary>
    /// Maps <see cref="Post"/> to its own column names. <c>Post.Id</c> is deliberately not mapped: Posts.Id is an identity
    /// column and the contract never writes identity columns (a provider without an "ignore identity" mode, such as an
    /// array-bound INSERT, would otherwise write Id = 0 for every row).
    /// </summary>
    public class PostMapping : ModelMapping<Post>
    {
        public PostMapping()
        {
            Map(post => post.BlogId).ToDestinationColumn("BlogId");
            Map(post => post.Title).ToDestinationColumn("Title");
            Map(post => post.Content).ToDestinationColumn("Content");
        }
    }
}

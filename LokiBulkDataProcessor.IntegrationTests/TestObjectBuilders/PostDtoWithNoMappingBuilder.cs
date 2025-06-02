using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostDtoWithNoMappingBuilder
    {
        private PostDtoWithNoMapping _post = new PostDtoWithNoMapping();
        
        public PostDtoWithNoMappingBuilder WithTitle(string title)
        {
            _post.Title = title;
            return this;
        }
        public PostDtoWithNoMappingBuilder WithContent(string content)
        {
            _post.Content = content;
            return this;
        }

        public PostDtoWithNoMappingBuilder WithBlogId(int blogId)
        {
            _post.BlogId = blogId;
            return this;
        }

        public PostDtoWithNoMapping Build()
        {
            return _post;
        }
    }
}

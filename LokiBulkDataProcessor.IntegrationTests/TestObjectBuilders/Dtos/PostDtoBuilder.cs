using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostDtoBuilder
    {
        private string _title;
        private string _content;
        private int _blogId;

        public PostDtoBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public PostDtoBuilder WithContent(string content)
        {
            _content = content;
            return this;
        }

        public PostDtoBuilder WithBlogId(int blogId)
        {
            _blogId = blogId;
            return this;
        }

        public PostDto Build()
        {
            return new PostDto
            {
                TitleB = _title,
                ContentA = _content,
                ABlogId = _blogId,
            };
        }
    }
}

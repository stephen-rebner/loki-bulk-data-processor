using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostBuilder
    {
        private Post _post = new Post();

        public PostBuilder BuildFromPostDto(PostDto postDto)
        {
             _post = new Post
             {
                Title = postDto.Title,
                Content = postDto.Content
             };
            return this;
        }

        public PostBuilder WithTitle(string title)
        {
            _post.Title = title;
            return this;
        }
        public PostBuilder WithContent(string content)
        {
            _post.Content = content;
            return this;
        }

        public PostBuilder WithBlog(Blog blog)
        {
            _post.Blog = blog;
            return this;
        }

        public PostBuilder WithId(int id)
        {
            _post.Id = id;
            return this;
        }

        public Post Build()
        {
            return _post;
        }
    }
}

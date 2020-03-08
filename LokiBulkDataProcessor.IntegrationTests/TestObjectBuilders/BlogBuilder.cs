using LokiBulkDataProcessor.IntegrationTests.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class BlogBuilder
    {
        private int _id;
        private string _url;

        public BlogBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public BlogBuilder WithUrl(string url)
        {
            _url = url;
            return this;
        }

        public Blog Build()
        {
            return new Blog
            {
                Id = _id,
                Url = _url
            };
        }
    }
}

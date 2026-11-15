using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.TestObjectBuilders
{
    public class BlogBuilder
    {
        private string _url;

        public BlogBuilder WithUrl(string url)
        {
            _url = url;
            return this;
        }

        public Blog Build()
        {
            return new Blog
            {
                Url = _url
            };
        }
    }
}

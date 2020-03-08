namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public static class TestObjectFactory
    {
        public static TestDbModelBuilder TestDbModelObject()
        {
            return new TestDbModelBuilder();
        }

        public static TestDataTableBuilder NewTestDataTable()
        {
            return new TestDataTableBuilder();
        }

        public static PostDataTableBuilder NewPostDataTable()
        {
            return new PostDataTableBuilder();
        }

        public static BlogBuilder NewBlog()
        {
            return new BlogBuilder();
        }

        public static PostBuilder NewPost()
        {
            return new PostBuilder();
        }

        public static PostDtoBuilder NewPostDto()
        {
            return new PostDtoBuilder();
        }
    }
}

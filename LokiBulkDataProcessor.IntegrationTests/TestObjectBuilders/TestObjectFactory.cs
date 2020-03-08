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
    }
}

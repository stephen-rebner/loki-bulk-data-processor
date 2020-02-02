namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public static class TestObjectFactory
    {
        public static TestDbModelObjectBuilder NewTestDbModel()
        {
            return new TestDbModelObjectBuilder();
        }

        public static TestDataTableBuilder NewTestDataTable()
        {
            return new TestDataTableBuilder();
        }
    }
}

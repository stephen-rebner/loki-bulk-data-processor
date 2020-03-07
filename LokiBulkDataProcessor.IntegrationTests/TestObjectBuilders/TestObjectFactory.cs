namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public static class TestObjectFactory
    {
        public static ColsInDiffOrderObjectBuilder NewColsInDiffOrderObject()
        {
            return new ColsInDiffOrderObjectBuilder();
        }

        public static TestDataTableBuilder NewTestDataTable()
        {
            return new TestDataTableBuilder();
        }
    }
}

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public static class TestObjectFactory
    {
        public static TestDbModelObjectBuilder NewTestDbModel()
        {
            return new TestDbModelObjectBuilder();
        }
    }
}

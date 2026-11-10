namespace LokiBulkDataProcessor.UnitTests.TestModels
{
    public class ModelWithComputedProperty
    {
        public int Id { get; set; }

        public string Reference => $"REF-{Id}";
    }
}

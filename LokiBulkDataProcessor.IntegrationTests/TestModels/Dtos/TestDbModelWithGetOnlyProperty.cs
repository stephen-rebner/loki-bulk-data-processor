namespace LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos
{
    /// <summary>
    /// A <see cref="TestDbModel"/> with a computed, get-only property that has no matching column (#19).
    /// </summary>
    public class TestDbModelWithGetOnlyProperty : TestDbModel
    {
        public string Description => $"{Id}: {StringColumn}";
    }
}

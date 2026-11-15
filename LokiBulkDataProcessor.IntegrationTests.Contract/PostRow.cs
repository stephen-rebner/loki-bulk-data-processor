namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>A row read back from the Posts table by a provider's <see cref="BulkProcessorContractTests.ReadPostsAsync"/>.</summary>
public sealed record PostRow(int Id, int BlogId, string Title, string Content);

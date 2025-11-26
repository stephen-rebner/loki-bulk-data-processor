namespace LokiBulkDataProcessor.Core.Interfaces
{
    /// <summary>
    /// Interface for document/NoSQL database bulk processors (CosmosDB, MongoDB, etc.)
    /// Provides document-oriented bulk operations
    /// </summary>
    public interface IDocumentBulkProcessor : IBulkProcessor
    {
        // Document databases typically work with:
        // 1. Strongly-typed objects (IEnumerable<T>) - inherited from IBulkProcessor
        // 2. JSON streams - inherited from IBulkProcessor
        // No additional methods needed beyond IBulkProcessor for now
    }
}

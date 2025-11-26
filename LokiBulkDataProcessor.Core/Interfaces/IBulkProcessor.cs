using System.Data;

namespace LokiBulkDataProcessor.Core.Interfaces
{
    /// <summary>
    /// Base interface for all bulk data processors
    /// </summary>
    public interface IBulkProcessor
    {
        int Timeout { get; set;}

        int BatchSize { get; set;}
        
        string ConnectionString { get; set; }

        Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationName) where T : class;

        Task SaveAsync(Stream jsonStream);
    }
}

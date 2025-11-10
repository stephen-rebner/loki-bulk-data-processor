using System.Data;

namespace LokiBulkDataProcessor.Core.Interfaces
{
    public interface IBulkProcessor
    {
        int Timeout { get; set;}

        int BatchSize { get; set;}

        IDbTransaction Transaction { get; set; }
        
        string ConnectionString { get; set; }

        IBulkProcessor WithConnectionString(string connectionString);

        Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class;

        Task SaveAsync(DataTable dataTable, string destinationTableName);
        
        Task SaveAsync(IDataReader dataReader, string destinationTableName);

        Task SaveAsync(Stream jsonStream);
    }
}

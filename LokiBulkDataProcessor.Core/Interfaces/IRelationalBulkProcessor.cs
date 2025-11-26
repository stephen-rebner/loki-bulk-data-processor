using System.Data;

namespace LokiBulkDataProcessor.Core.Interfaces
{
    /// <summary>
    /// Interface for relational database bulk processors (SQL Server, PostgreSQL, etc.)
    /// Extends IBulkProcessor with relational-specific operations
    /// </summary>
    public interface IRelationalBulkProcessor : IBulkProcessor
    {
        Task SaveAsync(DataTable dataTable, string destinationTableName);
        
        Task SaveAsync(IDataReader dataReader, string destinationTableName);
    }
}

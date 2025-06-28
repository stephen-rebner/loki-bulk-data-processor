using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public interface IBulkProcessor
    {
        int Timeout { get; set;}

        int BatchSize { get; set;}

        IDbTransaction Transaction { get; set; }

        IBulkProcessor WithConnectionString(string connectionString);

        Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class;

        Task SaveAsync(DataTable dataTable, string destinationTableName);
        
        Task SaveAsync(IDataReader dataReader, string destinationTableName);

        Task SaveAsync(Stream jsonStream);
    }
}

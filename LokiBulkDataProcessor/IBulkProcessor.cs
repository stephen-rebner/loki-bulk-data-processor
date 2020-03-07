using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public interface IBulkProcessor
    {
        int Timeout { get; set;}
        int BatchSize { get; set;}
        Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class;
        Task SaveAsync(DataTable dataTable, string destinationTableName);
    }
}

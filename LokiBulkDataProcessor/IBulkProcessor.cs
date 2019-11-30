using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public interface IBulkProcessor
    {
        int Timeout { get; set;}
        int BatchSize { get; set;}
        Task<bool> DeleteAsync<T>(IEnumerable<T> data);
        Task<bool> SaveAsync<T>(IEnumerable<T> dataToProcess, string connectionString, string destinationTableName);
        Task<IEnumerable<T>> UpdateAsync<T>(IEnumerable<T> data);
    }
}

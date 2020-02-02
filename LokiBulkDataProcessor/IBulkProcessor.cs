using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public interface IBulkProcessor
    {
        int Timeout { get; set;}
        int BatchSize { get; set;}
        string DestinationTableName { get; set; }
        Task SaveAsync<T>(IEnumerable<T> dataToProcess);
        Task SaveAsync(DataTable dataTable);
    }
}

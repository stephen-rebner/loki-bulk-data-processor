using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Repository
{
    public interface IBulkRepository
    {
        Task<bool> DeleteAsync<T>(IEnumerable<T> data);
        Task<bool> SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName);
        Task<IEnumerable<T>> UpdateAsync<T>(IEnumerable<T> data);
    }
}

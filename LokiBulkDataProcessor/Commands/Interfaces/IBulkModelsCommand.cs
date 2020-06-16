using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkModelsCommand
    {
        Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class;
    }
}

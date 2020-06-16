using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkDataTableCommand
    {
        Task Execute(DataTable dataToProcess, string destinationTableName);
    }
}

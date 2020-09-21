using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface ICopyToTempTableCommand
    {
        Task Copy(DataTable destinationTableInfo, DataTable dataToCopy, string destinationTableName);
    }
}

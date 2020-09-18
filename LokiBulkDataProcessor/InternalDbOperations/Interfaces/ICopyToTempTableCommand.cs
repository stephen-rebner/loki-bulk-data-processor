using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface ICopyToTempTableCommand
    {
        Task Copy(DataTable dataToCopy, string destinationTableName);
    }
}

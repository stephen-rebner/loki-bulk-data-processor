using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkCommand
    {
        Task ExecuteAsync();
    }
}

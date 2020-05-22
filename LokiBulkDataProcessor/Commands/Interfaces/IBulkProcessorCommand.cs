using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkProcessorCommand
    {
        Task Execute();
    }
}

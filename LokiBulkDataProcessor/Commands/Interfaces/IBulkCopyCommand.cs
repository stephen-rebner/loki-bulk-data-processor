using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkCopyCommand
    {
        Task Execute();
    }
}

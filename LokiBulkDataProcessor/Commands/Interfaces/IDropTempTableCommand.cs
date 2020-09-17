using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IDropTempTableCommand
    {
        void Execute();
    }
}

using Loki.BulkDataProcessor.Commands.Interfaces;

namespace Loki.BulkDataProcessor.Commands.Factory.Interface
{
    public interface IBulkCommandFactory
    {
        IBulkCommand NewCommand<T>() where T : IBulkCommand;
    }
}

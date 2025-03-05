using Loki.BulkDataProcessor.Commands.Interfaces;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkModelsCommand NewBulkCopyModelsCommand();

        IBulkDataTableCommand NewBulkCopyDataTableCommand();
        
        IBulkCopyFromDataReaderCommand NewBulkCopyDataReaderCommand();
    }
}

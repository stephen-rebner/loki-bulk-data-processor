using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    internal class CommandFactory(
        IAppContext appContext,
        ILokiDbConnection dbConnection,
        ILoggerFactory loggerFactory) : ICommandFactory
    {
        public IBulkModelsCommand NewBulkCopyModelsCommand()
        {
            return new BulkCopyModelsCommand(
                appContext, 
                dbConnection,
                loggerFactory.CreateLogger<BulkCopyModelsCommand>());
        }

        public IBulkDataTableCommand NewBulkCopyDataTableCommand()
        {
            return new BulkCopyDataTableCommand(
                appContext, 
                dbConnection,
                loggerFactory.CreateLogger<BulkCopyDataTableCommand>());
        }

        public IBulkCopyFromDataReaderCommand NewBulkCopyDataReaderCommand()
        {
            return new BulkCopyFromDataReaderCommand(
                appContext, 
                dbConnection,
                loggerFactory.CreateLogger<BulkCopyFromDataReaderCommand>());
        }
    }
}
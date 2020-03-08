using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    internal class CommandFactory : ICommandFactory
    {
        public IBulkCopyModelsCommand<T> NewBulkCopyModelsCommand<T>(
            int batchSize,
            int timeout,
            string tableName,
            string connectionString,
            IEnumerable<T> dataToCopy) where T : class
        {
            return new BulkCopyModelsCommand<T>(
                batchSize, 
                timeout, 
                tableName, 
                connectionString, 
                dataToCopy);
        }

        public IBulkCopyDataTableCommand NewBulkCopyDataTableCommand(
            int batchSize, 
            int timeout, 
            string tableName, 
            string connectionString, 
            DataTable dataToCopy)
        {
            return new BulkCopyDataTableCommand(
                batchSize,
                timeout,
                tableName,
                connectionString,
                dataToCopy);
        }
    }
}

using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkCopyModelsCommand<T> NewBulkCopyModelsCommand<T>(
            int batchSize,
            int timeout,
            string tableName,
            string connectionString,
            IEnumerable<T> dataToCopy) where T : class;

        IBulkCopyDataTableCommand NewBulkCopyDataTableCommand(
            int batchSize,
            int timeout,
            string tableName,
            string connectionString,
            DataTable dataToCopy);
    }
}

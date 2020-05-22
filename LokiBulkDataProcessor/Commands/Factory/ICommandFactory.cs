using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkProcessorModelsCommand<T> NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class;

        IBulkProcessorDataTableCommand NewBulkCopyDataTableCommand(DataTable dataToCopy, string tableName);
        IBulkProcessorDataTableCommand NewBulkUpdateDataTableCommand(DataTable dataToCopy, string tableName);
    }
}

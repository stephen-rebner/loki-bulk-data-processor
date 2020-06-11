using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkProcessorCommand NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class;

        IBulkProcessorDataTableCommand NewBulkCopyDataTableCommand(DataTable dataToCopy, string tableName);
        //IBulkProcessorCommand NewBulkUpdateDataTableCommand(DataTable dataToCopy, string tableName);
    }
}

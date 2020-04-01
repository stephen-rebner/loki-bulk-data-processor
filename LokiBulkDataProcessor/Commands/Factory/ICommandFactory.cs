using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkCopyModelsCommand<T> NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class;

        //IBulkCopyDataTableCommand NewBulkCopyDataTableCommand(
        //    string tableName,
        //    DataTable dataToCopy);
    }
}

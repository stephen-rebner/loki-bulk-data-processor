using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    internal class CommandFactory : ICommandFactory
    {
        private readonly IAppContext _appContext;
        private readonly ITempTable _tempTable;

        public CommandFactory(IAppContext appContext, ITempTable tempTable)
        {
            _appContext = appContext;
            _tempTable = tempTable;
        }

        public IBulkProcessorModelsCommand<T> NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            return new BulkCopyModelsCommand<T>(dataToCopy, tableName, _appContext);
        }

        public IBulkProcessorDataTableCommand NewBulkCopyDataTableCommand(DataTable dataToCopy, string tableName)
        {
            return new BulkCopyDataTableCommand(dataToCopy, tableName, _appContext);
        }

        //public IBulkProcessorCommand NewBulkUpdateDataTableCommand(DataTable dataToCopy, string tableName)
        //{
        //    return new BulkUpdateDataTableCommand(dataToCopy, tableName, _appContext, _tempTable);
        //}
    }
}

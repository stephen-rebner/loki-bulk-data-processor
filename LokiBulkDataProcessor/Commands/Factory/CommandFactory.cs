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
        private readonly ISqlCommand _sqlCommand;

        public CommandFactory(IAppContext appContext, IDbOperations dbOperations, ITempTable tempTable, ISqlCommand sqlCommand)
        {
            _appContext = appContext;
            _tempTable = tempTable;
            _sqlCommand = sqlCommand;
        }

        public IBulkProcessorCommand NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            return new BulkCopyModelsCommand<T>(dataToCopy, tableName, _appContext);
        }

        public IBulkProcessorCommand NewBulkCopyDataTableCommand(DataTable dataToCopy, string tableName)
        {
            return new BulkCopyDataTableCommand(dataToCopy, tableName, _appContext);
        }

        public IBulkProcessorCommand NewBulkUpdateDataTableCommand(DataTable dataToCopy, string tableName)
        {
            return new BulkUpdateDataTableCommand(dataToCopy, tableName, _appContext, _tempTable, _sqlCommand);
        }
    }
}

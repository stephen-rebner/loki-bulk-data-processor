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
        private readonly IDbOperations _dbOperations;

        public CommandFactory(IAppContext appContext, IDbOperations dbOperations)
        {
            _appContext = appContext;
            _dbOperations = dbOperations;
        }

        public IBulkProcessorCommand NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            return new BulkCopyModelsCommand<T>(dataToCopy, tableName, _appContext, _dbOperations);
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

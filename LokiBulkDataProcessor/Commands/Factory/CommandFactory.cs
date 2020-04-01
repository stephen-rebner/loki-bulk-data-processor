using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    internal class CommandFactory : ICommandFactory
    {
        private readonly IAppContext _appContext;

        public CommandFactory(IAppContext appContext)
        {
            _appContext = appContext;
        }

        public IBulkCopyModelsCommand<T> NewBulkCopyModelsCommand<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            return new BulkCopyModelsCommand<T>(dataToCopy, tableName, _appContext);
        }

        //public IBulkCopyDataTableCommand NewBulkCopyDataTableCommand(
        //    int batchSize, 
        //    int timeout, 
        //    string tableName, 
        //    string connectionString, 
        //    DataTable dataToCopy)
        //{
        //    return new BulkCopyDataTableCommand(
        //        batchSize,
        //        timeout,
        //        tableName,
        //        connectionString,
        //        dataToCopy);
        //}
    }
}

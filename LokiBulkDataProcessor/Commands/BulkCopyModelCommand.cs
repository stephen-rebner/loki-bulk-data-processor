using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand : IBulkModelsCommand
    {
        private readonly IAppContext _appContext;
        private readonly ILokiDbConnection _dbConnection;

        public BulkCopyModelsCommand(IAppContext appContext, ILokiDbConnection sqlDbConnection)
        {
            _appContext = appContext;
            _dbConnection = sqlDbConnection;
        }

        public async Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            _dbConnection.Init();
            var transaction = _dbConnection.BeginTransactionIfUsingInternalTransaction();

            try
            {
                var type = typeof(T);
                var mapping = _appContext.ModelMappingCollection.GetMappingFor(type);
                var propertyNames = type.GetPublicPropertyNames();

                using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);

                bulkCopyCommand.MapColumns(mapping, propertyNames);
                await bulkCopyCommand.WriteToServerAsync(dataToProcess, propertyNames, destinationTableName);

                transaction.CommitIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                transaction.DisposeIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                _dbConnection.DisposeIfUsingInternalTransaction();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
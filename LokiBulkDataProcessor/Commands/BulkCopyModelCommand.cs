using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand : IBulkModelsCommand
    {
        private readonly IAppContext _appContext;
        private readonly ISqlDbConnection _dbConnection;

        public BulkCopyModelsCommand(IAppContext appContext, ISqlDbConnection sqlDbConnection)
        {
            _appContext = appContext;
            _dbConnection = sqlDbConnection;
        }

        public async Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            using (_dbConnection)
            {
                _dbConnection.Open();
                using var transaction = _dbConnection.BeginTransaction();

                try
                {
                    var type = typeof(T);
                    var mapping = _appContext.ModelMappingCollection.GetMappingFor(type);
                    var propertyNames = type.GetPublicPropertyNames();

                    using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);

                    bulkCopyCommand.MapColumns(mapping, propertyNames);
                    await bulkCopyCommand.WriteToServerAsync(dataToProcess, propertyNames, destinationTableName);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
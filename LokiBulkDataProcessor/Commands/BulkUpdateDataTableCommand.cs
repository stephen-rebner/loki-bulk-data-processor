using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.SqlBuilders;
using System.Data;

using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkUpdateDataTableCommand : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ISqlDbConnection _dbConnection;

        public BulkUpdateDataTableCommand(IAppContext appContext, ISqlDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            using (_dbConnection)
            {
                _dbConnection.Open();
                using var transaction = _dbConnection.BeginTransaction();

                try
                {
                    using var query = _dbConnection.CreateQuery(transaction);

                    var destinationTableInfo = query.Load(TableInfo.GenerateDatabaseTableInfoQuery(destinationTableName, _dbConnection.Database));

                    var copyToTempTableCommand = _dbConnection.CreateNewCopyToTempTableCommand(transaction);

                    await copyToTempTableCommand.Copy(destinationTableInfo, dataToCopy, destinationTableName);

                    using var updateCommand = _dbConnection.CreateCommand(
                        UpdateJoinOnPrimaryKeySql.Generate(destinationTableInfo, destinationTableName), transaction);

                    updateCommand.ExecuteNonQuery();

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

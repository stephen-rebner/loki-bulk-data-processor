using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ILokiDbConnection _dbConnection;

        public BulkCopyDataTableCommand(IAppContext appContext, ILokiDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            _dbConnection.Init();
            var transaction = _dbConnection.BeginTransactionIfUsingInternalTransaction();

            try
            {
                var mapping = _appContext.DataTableMappingCollection.GetMappingFor(dataToCopy.TableName);
                var columnNames = dataToCopy.Columns.Cast<DataColumn>()
                                    .Select(x => x.ColumnName)
                                    .ToArray();

                using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);

                bulkCopyCommand.MapColumns(mapping, columnNames);
                await bulkCopyCommand.WriteToServerAsync(dataToCopy, destinationTableName);

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

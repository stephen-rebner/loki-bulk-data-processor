using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.SqlBuilders;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class CopyToTempTableCommand : ICopyToTempTableCommand
    {
        private readonly ISqlDbConnection _dbConnection;
        private readonly IAppContext _appContext;
        private readonly IDbTransaction _transaction;

        public CopyToTempTableCommand(ISqlDbConnection dbConnection, IAppContext appContext, IDbTransaction transaction)
        {
            _dbConnection = dbConnection;
            _appContext = appContext;
            _transaction = transaction;
        }

        public async Task Copy(DataTable dataToCopy, string destinationTableName)
        {
            using var query = _dbConnection.CreateQuery(_transaction);
            // todo: write unit test for query builder below
            var tableInfoDataTable = query.Load(TableInfo.GenerateDatabaseTableInfoQuery(destinationTableName, _dbConnection.Database));

            using var createTempTableCommand = _dbConnection.CreateCommand(
                TempTable.GenerateCreateStatement(tableInfoDataTable), _transaction);

            createTempTableCommand.ExecuteNonQuery();

            var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);
            var columnNames = dataToCopy.Columns.Cast<DataColumn>().Select(x => x.ColumnName);

            using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand(_transaction);
            bulkCopyCommand.MapNonPrimaryKeyColumns(mapping, columnNames);
            bulkCopyCommand.MapPrimaryKey(mapping);

            await bulkCopyCommand.WriteToServerAsync(dataToCopy, DbConstants.TempTableName);
        }
    }
}

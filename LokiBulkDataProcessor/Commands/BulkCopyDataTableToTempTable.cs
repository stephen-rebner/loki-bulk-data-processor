using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.SqlBuilders;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    class BulkCopyDataTableToTempTable : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ISqlDbConnection _dbConnection;

        public BulkCopyDataTableToTempTable(IAppContext appContext, ISqlDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            using(_dbConnection)
            {
                try
                {
                    _dbConnection.Open();

                    using var query = _dbConnection.CreateQuery();
                    // todo: write unit test for query builder below
                    var tableInfoDataTable = query.Load(SchemaQuery.GenerateDataTableInfoQuery(destinationTableName, _dbConnection.Database));

                    using var createTempTableCommand = _dbConnection.CreateCommand(
                       TempTableSqlGenerator.GenerateCreateStatement(tableInfoDataTable), null);

                    createTempTableCommand.ExecuteNonQuery();

                    var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);
                    var columnNames = dataToCopy.Columns.Cast<DataColumn>().Select(x => x.ColumnName);

                    using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand(null);
                    bulkCopyCommand.MapNonPrimaryKeyColumns(mapping, columnNames);
                    bulkCopyCommand.MapPrimaryKey(mapping);

                    await bulkCopyCommand.WriteToServerAsync(dataToCopy, DbConstants.TempTableName);
                }
                catch(Exception e)
                {
                    throw;
                }
                
            }
        }
    }
}

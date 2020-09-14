using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.SqlBuilders;
using System;
using System.Data;
using System.Linq;
using System.Text;
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
                    var tableInfoDataTable = LoadDestinationTableInfo(destinationTableName, transaction);

                    CreateTempTable(tableInfoDataTable, transaction);

                    await CopyDataToTempTableAsync(destinationTableName, dataToCopy, transaction);

                    //var batches = Math.Ceiling((double)dataToCopy.Rows.Count / _appContext.BatchSize);

                    //for (var i = 1; i <= batches; i++)
                    //{
                    //    var commandText = BuildUpdateStatement(mapping, destinationTableName);
                    //    using var saveCommand = (SqlCommand)_dbConnection.CreateCommand(commandText, null);
                    //    await saveCommand.ExecuteNonQueryAsync();
                    //}

                    transaction.Commit();
                }
                catch(Exception e)
                {
                    //transaction.Rollback();
                    throw;
                }
            }
        }

        private DataTable LoadDestinationTableInfo(string destinationTableName, IDbTransaction transaction)
        {
            using var query = _dbConnection.CreateQuery(transaction);
            // todo: write unit test for query builder below
            return query.Load(SchemaQuery.GenerateDataTableInfoQuery(destinationTableName, _dbConnection.Database));
        }

        private void CreateTempTable(DataTable tableInfoDataTable, IDbTransaction transaction)
        {
            using var createTempTableCommand = _dbConnection.CreateCommand(
                       TempTableSqlGenerator.GenerateCreateStatement(tableInfoDataTable), transaction);

            createTempTableCommand.ExecuteNonQuery();
        }

        private async Task CopyDataToTempTableAsync(string destinationTableName, DataTable dataToCopy, IDbTransaction transaction)
        {
            var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);
            var columnNames = dataToCopy.Columns.Cast<DataColumn>().Select(x => x.ColumnName);

            using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand(transaction);
            bulkCopyCommand.MapColumns(mapping, columnNames);
            // todo: create map column method on bulk copy command and call here to add primary key
            await bulkCopyCommand.WriteToServerAsync(dataToCopy, DbConstants.TempTableName);
        }

        private string BuildUpdateStatement(AbstractMapping mapping, string destinationTableName)
        {
            var primaryKeyColumn = mapping.MappingInfo.MappingMetaDataCollection.FirstOrDefault(metaData => metaData.IsIdentityColumn);
            var columnsToUpdate = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsIdentityColumn).Select(x => x.DestinationColumn);

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine($"UPDATE dest");
            sqlBuilder.AppendLine("SET ");

            foreach(var columnName in columnsToUpdate)
            {

                sqlBuilder.Append($"dest.{columnName} = t.{columnName}");
                
                if(!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.AppendLine(", ");
                }
            }

            sqlBuilder.AppendLine($"FROM {destinationTableName} dest");

            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON t.{primaryKeyColumn.DestinationColumn} = dest.{primaryKeyColumn.DestinationColumn}");

            return sqlBuilder.ToString();
        }
    }
}

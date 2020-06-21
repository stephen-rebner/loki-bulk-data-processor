using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.SqlBuilders;
using System;
using System.Data;
using System.Data.SqlClient;
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
                    var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);

                    ThrowExecptionIfMappingIsNull(mapping);

                    var columnNames = mapping.MappingInfo.MappingMetaDataCollection.Select(metaData => metaData.DestinationColumn);

                    using var tempTableCommand = (SqlCommand)_dbConnection.CreateCommand(TempTable.GenerateCreateSqlStatement(columnNames), (SqlTransaction)transaction);
                    await tempTableCommand.ExecuteNonQueryAsync();

                    using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
                    
                    await bulkCopyCommand.WriteToServerAsync(dataToCopy, DbConstants.TempTableName);

                    var batches = Math.Ceiling((double)dataToCopy.Rows.Count / _appContext.BatchSize);

                    for (var i = 1; i <= batches; i++)
                    {
                        var commandText = BuildUpdateStatement(mapping, destinationTableName);
                        using var saveCommand = (SqlCommand)_dbConnection.CreateCommand(commandText, (SqlTransaction)transaction);
                        await saveCommand.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void ThrowExecptionIfMappingIsNull(AbstractMapping mapping)
        {
            if(mapping == null || !mapping.MappingInfo.MappingMetaDataCollection.Any(metaData => metaData.IsPrimaryKey))
            {
                throw new MappingException("A mapping is required for bulk updates which has a primary key specified.");
            }
        }

        private string BuildUpdateStatement(AbstractMapping mapping, string destinationTableName)
        {
            var primaryKeyColumn = mapping.MappingInfo.MappingMetaDataCollection.FirstOrDefault(metaData => metaData.IsPrimaryKey);
            var columnsToUpdate = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsPrimaryKey).Select(x => x.DestinationColumn);

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine($"UPDATE dest");
            sqlBuilder.AppendLine("SET ");

            foreach(var columnName in columnsToUpdate)
            {

                sqlBuilder.AppendLine($"dest.{columnName} = t.{columnName}");
                
                if(columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.Append(", ");
                }
            }

            sqlBuilder.AppendLine($"FROM {destinationTableName} dest");

            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON t.{primaryKeyColumn.DestinationColumn} = dest.{primaryKeyColumn.DestinationColumn}");

            return sqlBuilder.ToString();
        }
    }
}

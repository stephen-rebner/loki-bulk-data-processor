using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
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

        private string BuildUpdateStatement(DataTable destinationTableInfo, string destinationTableName)
        {
            var primaryKeys = destinationTableInfo
                .AsEnumerable()
                .Where(row => row.Field<string>("CONSTRAINT_TYPE") != null && row.Field<string>("CONSTRAINT_TYPE").Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row => row.Field<string>("COLUMN_NAME"))
                .ToArray();

            var columnsToUpdate = destinationTableInfo
                .AsEnumerable()
                .Where(row => row.Field<string>("CONSTRAINT_TYPE") == null || !row.Field<string>("CONSTRAINT_TYPE").Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row => row.Field<string>("COLUMN_NAME"))
                .ToArray(); ;

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine($"UPDATE dest");
            sqlBuilder.AppendLine("SET ");

            foreach(var columnName in columnsToUpdate)
            {
                sqlBuilder.Append($"   dest.{columnName} = t.{columnName}");
                
                if(!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.AppendLine(", ");
                }
                else
                {
                    sqlBuilder.AppendLine();
                }
            }

            sqlBuilder.AppendLine($"FROM {destinationTableName} dest");

            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON ");

            foreach (var primaryKey in primaryKeys)
            {
                if (!primaryKeys.First().Equals(primaryKey, StringComparison.Ordinal))
                {
                    sqlBuilder.Append("AND ");
                }

                sqlBuilder.Append($"t.{primaryKey} = dest.{primaryKey} ");
            }

            sqlBuilder.Append("WHERE ");

            foreach(var columnName in columnsToUpdate)
            {
                sqlBuilder.Append($"t.{columnName} != dest.{columnName} ");

                if (!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.Append("OR ");
                }
            }

            return sqlBuilder.ToString();
        }
    }
}

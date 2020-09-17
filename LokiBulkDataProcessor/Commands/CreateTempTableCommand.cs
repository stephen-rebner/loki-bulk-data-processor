using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.SqlBuilders;
using System.Data;

namespace Loki.BulkDataProcessor.Commands
{
    internal class CreateTempTableCommand : ICreateTempTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ISqlDbConnection _dbConnection;

        public CreateTempTableCommand(IAppContext appContext, ISqlDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        /// <summary>
        /// Creates a global temp table which data can be copied to
        /// when performing updates or deletes.
        /// 
        /// It first of all queries the database to figure out the column data types
        /// of the destination table before creating the table itself.
        /// </summary>
        /// <param name="destinationTableName">The name of the destination table,</param>
        public void Execute(string destinationTableName)
        {
            using(_dbConnection)
            {
                _dbConnection.Open();

                var destinationTableInfo = LoadDestinationTableInfo(destinationTableName);

                CreateTempTable(destinationTableInfo);
            }
        }

        private DataTable LoadDestinationTableInfo(string destinationTableName)
        {
            using var query = _dbConnection.CreateQuery();
            // todo: write unit test for query builder below
            return query.Load(SchemaQuery.GenerateDataTableInfoQuery(destinationTableName, _dbConnection.Database));
        }

        private void CreateTempTable(DataTable tableInfoDataTable)
        {
            using var createTempTableCommand = _dbConnection.CreateCommand(
                       TempTableSqlGenerator.GenerateCreateStatement(tableInfoDataTable), null);

            createTempTableCommand.ExecuteNonQuery();
        }
    }
}

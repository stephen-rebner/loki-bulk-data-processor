using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.SqlBuilders;

namespace Loki.BulkDataProcessor.Commands
{
    internal class DropTempTableCommand : IDropTempTableCommand
    {
        private readonly ISqlDbConnection _dbConnection;

        public DropTempTableCommand(ISqlDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public void Execute()
        {
            using(_dbConnection)
            {
                _dbConnection.Open();

                using var command = _dbConnection.CreateCommand(TempTableSqlGenerator.GenerateDropStatement(), null);

                command.ExecuteNonQuery();
            }
        }
    }
}

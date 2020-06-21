using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface ISqlDbConnection : IDbConnection
    {
        IBulkCopyCommand CreateNewBulkCopyCommand(SqlTransaction transaction);

        IDbCommand CreateCommand(string commandText, SqlTransaction transaction);
    }
}

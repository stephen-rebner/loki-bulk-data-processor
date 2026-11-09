using System.Data;
using Microsoft.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface ILokiDbConnection : IDbConnection
    {
        IDbTransaction BeginTransactionIfUsingInternalTransaction();

        IBulkCopyCommand CreateNewBulkCopyCommand(SqlTransaction transaction);

        IDbCommand CreateCommand(string commandText, SqlTransaction transaction);

        void DisposeIfUsingInternalTransaction();
    }
}

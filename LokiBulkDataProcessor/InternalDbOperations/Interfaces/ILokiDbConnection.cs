using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface ILokiDbConnection : IDbConnection
    {
        IDbTransaction BeginTransactionIfUsingInternalTransaction();

        IBulkCopyCommand CreateNewBulkCopyCommand(SqlTransaction transaction);

        IDbCommand CreateCommand(string commandText, SqlTransaction transaction);

        void DisposeIfUsingInternalTransaction();

        void Init();
    }
}

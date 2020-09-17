using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface ISqlDbConnection : IDbConnection
    {
        IBulkCopyCommand CreateNewBulkCopyCommand(IDbTransaction transaction);

        IDbCommand CreateCommand(string commandText, IDbTransaction transaction);

        IQuery CreateQuery();
    }
}

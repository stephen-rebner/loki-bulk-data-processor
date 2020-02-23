using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Loki.BulkDataProcessor.Context.Interface
{
    public interface IDbContext: IDisposable
    {
        int Timeout { get; }

        int BatchSize { get; }

        string DestinationTableName { get; }

        void UpdateTimeout(int timeout);

        void UpdateDestinationTableName(string destinationTableName);

        void UpdateBatchSize(int batchSize);

        void UpdateConnectionString(string connectionString);

        SqlConnection OpenSqlConnection();
    }
}

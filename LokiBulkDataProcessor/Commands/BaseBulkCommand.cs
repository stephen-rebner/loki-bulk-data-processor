using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands
{
    internal abstract class BaseBulkCommand
    {
        protected int BatchSize { get; set; }
        protected int Timeout { get; set; }
        protected string TableName { get; set; }
        protected string ConnectionString { get; set; }

        protected void SetUpSqlBulkCopy(SqlBulkCopy sqlBulkCopy)
        {
            sqlBulkCopy.BatchSize = BatchSize;
            sqlBulkCopy.BulkCopyTimeout = Timeout;
            sqlBulkCopy.DestinationTableName = TableName;
        }
    }
}

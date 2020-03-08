using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : BaseBulkCommand, IBulkCopyDataTableCommand
    {
        public DataTable DataToCopy { get ; set ; }

        public BulkCopyDataTableCommand(
            int batchSize, 
            int timeout, 
            string tableName, 
            string connectionString, 
            DataTable dataToCopy)
        {
            BatchSize = batchSize;
            Timeout = timeout;
            TableName = tableName;
            DataToCopy = dataToCopy;
            ConnectionString = connectionString;
        }

        public async Task Execute()
        {
            using var sqlBulkCopy = new SqlBulkCopy(ConnectionString, SqlBulkCopyOptions.CheckConstraints);

            SetUpSqlBulkCopy(sqlBulkCopy);

            foreach (DataColumn column in DataToCopy.Columns)
            {
                sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await sqlBulkCopy.WriteToServerAsync(DataToCopy);
        }
    }
}

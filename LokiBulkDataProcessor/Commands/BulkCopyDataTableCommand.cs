using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : IBulkCopyDataTableCommand
    {
        public DataTable DataToCopy { get ; set ; }

        public int BatchSize { get; set; }

        public int Timeout { get; set; }

        public string TableName { get; set; }

        public string ConnectionString { get; set; }


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
            using var sqlConnection = new SqlConnection(ConnectionString);
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();

            sqlBulkCopy.BatchSize = BatchSize;
            sqlBulkCopy.BulkCopyTimeout = Timeout;
            sqlBulkCopy.DestinationTableName = TableName;

            foreach (DataColumn column in DataToCopy.Columns)
            {
                sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await sqlBulkCopy.WriteToServerAsync(DataToCopy);
        }
    }
}

using Loki.BulkDataProcessor.Commands.Interfaces;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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
            DataTable dataToCopy) : base(batchSize, timeout, tableName, connectionString)
        {
            DataToCopy = dataToCopy;
        }

        public async Task Execute()
        {
            var columnNames = DataToCopy.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();

            AddMappings(columnNames);

            try
            {
                await SqlBulkCopy.WriteToServerAsync(DataToCopy);
                SaveTransaction();
            }
            catch(Exception e)
            {
                RollbackTransaction();
                ThrowException(e.Message);
            }
            finally
            {
                Dispose();
            }
        }
    }
}

using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : BaseBulkCommand, IBulkCopyDataTableCommand
    {
        public DataTable DataToCopy { get; set; }

        public BulkCopyDataTableCommand(DataTable dataToCopy, string tableName, IAppContext appContext) 
            : base(appContext, tableName)
        {
            DataToCopy = dataToCopy;
        }

        public async Task Execute()
        {
            try
            {
                var mapping = _appContext.DataTableMappingCollection.GetMappingFor(_tableName);
                var columnNames = DataToCopy.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();

                MapColumns(mapping, columnNames);
                await SqlBulkCopy.WriteToServerAsync(DataToCopy);
                CommitTransaction();
            }
            catch (Exception e)
            {
                RollbackTransaction();
                ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                Dispose();
            }
        }
    }
}

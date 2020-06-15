using Loki.BulkDataProcessor.Commands.Abstract;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : BaseBulkProcessorCommand, IBulkProcessorCommand
    {
        private readonly DataTable _dataToCopy;

        public BulkCopyDataTableCommand(DataTable dataToCopy, string tableName, IAppContext appContext) 
            : base(appContext, tableName)
        {
            _dataToCopy = dataToCopy;
        }

        public async Task Execute()
        {
            try
            {
                var mapping = _appContext.DataTableMappingCollection.GetMappingFor(_dataToCopy.TableName);
                var columnNames = _dataToCopy.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();

                MapColumns(mapping, columnNames);
                await SqlBulkCopy.WriteToServerAsync(_dataToCopy);
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

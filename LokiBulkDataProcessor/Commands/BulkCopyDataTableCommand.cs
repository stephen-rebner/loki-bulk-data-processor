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
    internal class BulkCopyDataTableCommand : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;

        public BulkCopyDataTableCommand(IAppContext appContext)
        {
            _appContext = appContext;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            try
            {
                var mapping = _appContext.DataTableMappingCollection.GetMappingFor(dataToCopy.TableName);
                var columnNames = dataToCopy.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();

                //MapColumns(mapping, columnNames);
                //await SqlBulkCopy.WriteToServerAsync(dataToCopy);
                //CommitTransaction();
            }
            catch (Exception e)
            {
                //RollbackTransaction();
                //ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                //Dispose();
            }
        }
    }
}

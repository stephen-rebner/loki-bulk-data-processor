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
        private readonly IDbConnection _dbConnection;

        public BulkCopyDataTableCommand(IAppContext appContext, IDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            try
            {
                var mapping = _appContext.DataTableMappingCollection.GetMappingFor(dataToCopy.TableName);
                var columnNames = dataToCopy.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();

                using(_dbConnection)
                {
                    _dbConnection.Open();
                    using var transaction = _dbConnection.BeginTransaction();

                    var test = "test";
                }

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

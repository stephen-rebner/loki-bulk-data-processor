using System;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ILokiDbConnection _dbConnection;
        private readonly ILogger<BulkCopyDataTableCommand> _logger;
    
        public BulkCopyDataTableCommand(IAppContext appContext, ILokiDbConnection dbConnection, ILogger<BulkCopyDataTableCommand> logger = null)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
            _logger = logger ?? NullLogger<BulkCopyDataTableCommand>.Instance;
        }
    
        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            _logger.LogInformation("Starting bulk copy of DataTable {SourceTable} with {RowCount} rows to {DestinationTable}", 
                dataToCopy.TableName, dataToCopy.Rows.Count, destinationTableName);
            
            _dbConnection.Init();
            var transaction = _dbConnection.BeginTransactionIfUsingInternalTransaction();
            _logger.LogInformation("Transaction initialized for bulk copy operation");
    
            try
            {
                var mapping = _appContext.DataMappingCollection.GetMappingFor(dataToCopy.TableName);
                var columnNames = dataToCopy.Columns.Cast<DataColumn>()
                                    .Select(x => x.ColumnName)
                                    .ToArray();
                
                _logger.LogInformation("Mapped {ColumnCount} columns for bulk copy operation", columnNames.Length);
    
                using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
    
                bulkCopyCommand.MapColumns(mapping, columnNames);
                _logger.LogInformation("Beginning data transfer to server");
                await bulkCopyCommand.WriteToServerAsync(dataToCopy, destinationTableName);
    
                transaction.CommitIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                transaction.DisposeIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                _dbConnection.DisposeIfUsingInternalTransaction();
                
                _logger.LogInformation("Successfully completed bulk copy of {RowCount} rows to {DestinationTable}", 
                    dataToCopy.Rows.Count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of DataTable {SourceTable} to {DestinationTable}", 
                    dataToCopy.TableName, destinationTableName);
                transaction.Rollback();
                throw;
            }
        }
    }
}

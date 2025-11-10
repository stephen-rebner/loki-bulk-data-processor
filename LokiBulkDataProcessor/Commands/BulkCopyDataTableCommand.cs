using System;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Core.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.InternalDbOperations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyDataTableCommand(IAppContext appContext, ILogger<BulkCopyDataTableCommand> logger = null)
        : IBulkDataTableCommand
    {
        private readonly ILogger<BulkCopyDataTableCommand> _logger = logger ?? NullLogger<BulkCopyDataTableCommand>.Instance;

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            IDbTransaction transaction = null;
            
            try
            {
                _logger.LogInformation("Starting bulk copy of DataTable {SourceTable} with {RowCount} rows to {DestinationTable}", 
                    dataToCopy.TableName, dataToCopy.Rows.Count, destinationTableName);

                var dbConnection = LokiDbConnection.Create(appContext);

                transaction = dbConnection.BeginTransactionIfUsingInternalTransaction();
                
                _logger.LogInformation("Transaction initialized for bulk copy operation");
                
                var mapping = appContext.DataMappingCollection.GetMappingFor(dataToCopy.TableName);
                
                var columnNames = dataToCopy.Columns.Cast<DataColumn>()
                                    .Select(x => x.ColumnName)
                                    .ToArray();
                
                _logger.LogInformation("Mapped {ColumnCount} columns for bulk copy operation", columnNames.Length);
    
                using var bulkCopyCommand = dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
    
                bulkCopyCommand.MapColumns(mapping, columnNames);
                
                _logger.LogInformation("Beginning data transfer to server");
                
                await bulkCopyCommand.WriteToServerAsync(dataToCopy, destinationTableName);
    
                transaction.CommitIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
                
                transaction.DisposeIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
                
                dbConnection.DisposeIfUsingInternalTransaction();
                
                _logger.LogInformation("Successfully completed bulk copy of {RowCount} rows to {DestinationTable}", 
                    dataToCopy.Rows.Count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of DataTable {SourceTable} to {DestinationTable}", dataToCopy.TableName, destinationTableName);
                
                transaction?.Rollback();
                
                throw;
            }
        }
    }
}

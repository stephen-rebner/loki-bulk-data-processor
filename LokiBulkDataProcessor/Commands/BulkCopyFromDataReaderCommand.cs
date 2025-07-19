using System;
using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using System.Data.SqlClient;
using System.Linq;
using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands;

public class BulkCopyFromDataReaderCommand(IAppContext appContext, ILogger<BulkCopyFromDataReaderCommand> logger = null)
    : IBulkCopyFromDataReaderCommand
{
    private readonly ILogger<BulkCopyFromDataReaderCommand> _logger = logger ?? NullLogger<BulkCopyFromDataReaderCommand>.Instance;

    public async Task Execute(IDataReader dataReader, string destinationTableName)
    {
        IDbTransaction transaction = null;
        
        try
        {
            _logger.LogInformation("Starting bulk copy from IDataReader to table {TableName}", destinationTableName);

            var dbConnection = LokiDbConnection.Create(appContext);
            
            transaction = dbConnection.BeginTransactionIfUsingInternalTransaction();
            
            _logger.LogInformation("Transaction initialized for bulk copy operation");
            
            var mapping = appContext.DataMappingCollection.GetMappingFor(destinationTableName);
            
            var columnNames = Enumerable.Range(0, dataReader.FieldCount)
                .Select(dataReader.GetName)
                .ToArray();
            
            _logger.LogInformation("Mapped {ColumnCount} columns for bulk copy operation", columnNames.Length);
            
            using var bulkCopyCommand = dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
            
            bulkCopyCommand.MapColumns(mapping, columnNames);
            
            _logger.LogInformation("Beginning data transfer to server");
            
            await bulkCopyCommand.WriteToServerAsync(dataReader, destinationTableName);
            
            transaction.CommitIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
            
            transaction.DisposeIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
            
            dbConnection.DisposeIfUsingInternalTransaction();
            
            _logger.LogInformation("Successfully completed bulk copy from IDataReader to {TableName}", destinationTableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk copy from IDataReader to {TableName}", destinationTableName);
            transaction?.Rollback();
            throw;
        }
    }
}
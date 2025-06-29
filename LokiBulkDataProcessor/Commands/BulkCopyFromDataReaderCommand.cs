using System;
using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data.SqlClient;
using System.Linq;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands;

public class BulkCopyFromDataReaderCommand : IBulkCopyFromDataReaderCommand
{
    private readonly IAppContext _appContext;
    private readonly ILokiDbConnection _dbConnection;
    private readonly ILogger<BulkCopyFromDataReaderCommand> _logger;

    public BulkCopyFromDataReaderCommand(IAppContext appContext, ILokiDbConnection dbConnection, ILogger<BulkCopyFromDataReaderCommand> logger = null)
    {
        _appContext = appContext;
        _dbConnection = dbConnection;
        _logger = logger ?? NullLogger<BulkCopyFromDataReaderCommand>.Instance;
    }

    public async Task Execute(IDataReader dataReader, string destinationTableName)
    {
        _logger.LogInformation("Starting bulk copy from IDataReader to table {TableName}", destinationTableName);
        
        _dbConnection.Init();
        var transaction = _dbConnection.BeginTransactionIfUsingInternalTransaction();
        _logger.LogInformation("Transaction initialized for bulk copy operation");
        
        try
        {
            var mapping = _appContext.DataMappingCollection.GetMappingFor(destinationTableName);
            
            var columnNames = Enumerable.Range(0, dataReader.FieldCount)
                .Select(dataReader.GetName)
                .ToArray();
            
            _logger.LogInformation("Mapped {ColumnCount} columns for bulk copy operation", columnNames.Length);
            
            using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
            
            bulkCopyCommand.MapColumns(mapping, columnNames);
            _logger.LogInformation("Beginning data transfer to server");
            await bulkCopyCommand.WriteToServerAsync(dataReader, destinationTableName);
            
            transaction.CommitIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
            transaction.DisposeIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
            _dbConnection.DisposeIfUsingInternalTransaction();
            
            _logger.LogInformation("Successfully completed bulk copy from IDataReader to {TableName}", destinationTableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk copy from IDataReader to {TableName}", destinationTableName);
            transaction.Rollback();
            throw;
        }
    }
}
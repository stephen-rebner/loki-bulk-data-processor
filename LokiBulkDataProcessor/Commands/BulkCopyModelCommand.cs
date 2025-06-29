using System;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand : IBulkModelsCommand
    {
        private readonly IAppContext _appContext;
        private readonly ILokiDbConnection _dbConnection;
        private readonly ILogger<BulkCopyModelsCommand> _logger;
    
        public BulkCopyModelsCommand(IAppContext appContext, ILokiDbConnection sqlDbConnection, ILogger<BulkCopyModelsCommand> logger = null)
        {
            _appContext = appContext;
            _dbConnection = sqlDbConnection;
            _logger = logger ?? NullLogger<BulkCopyModelsCommand>.Instance;
        }
    
        public async Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            var count = dataToProcess is ICollection<T> collection ? collection.Count : dataToProcess.Count();
            _logger.LogInformation("Starting bulk copy of {Count} models of type {ModelType} to table {TableName}", 
                count, typeof(T).Name, destinationTableName);
            
            _dbConnection.Init();
            var transaction = _dbConnection.BeginTransactionIfUsingInternalTransaction();
            _logger.LogInformation("Transaction initialized for bulk copy operation");
    
            try
            {
                var type = typeof(T);
                var mapping = _appContext.ModelMappingCollection.GetMappingFor(type);
                var propertyNames = type.GetPublicPropertyNames();
                
                _logger.LogInformation("Mapped {PropertyCount} properties for bulk copy operation", propertyNames.Length);
    
                using var bulkCopyCommand = _dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
    
                bulkCopyCommand.MapColumns(mapping, propertyNames);
                _logger.LogInformation("Beginning data transfer to server");
                await bulkCopyCommand.WriteToServerAsync(dataToProcess, propertyNames, destinationTableName);
    
                transaction.CommitIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                transaction.DisposeIfUsingInternalTransaction(_appContext.IsUsingExternalTransaction);
                _dbConnection.DisposeIfUsingInternalTransaction();
                
                _logger.LogInformation("Successfully completed bulk copy of {Count} models to {TableName}", 
                    count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of {Count} models of type {ModelType} to {TableName}", 
                    count, typeof(T).Name, destinationTableName);
                transaction.Rollback();
                throw;
            }
        }
    }
}
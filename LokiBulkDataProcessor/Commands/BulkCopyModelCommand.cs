using System;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand(IAppContext appContext, ILogger<BulkCopyModelsCommand> logger = null)
        : IBulkModelsCommand
    {
        private readonly ILogger<BulkCopyModelsCommand> _logger = logger ?? NullLogger<BulkCopyModelsCommand>.Instance;

        public async Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            ILokiDbConnection dbConnection;
            IDbTransaction transaction = null;

            var count = dataToProcess is ICollection<T> collection ? collection.Count : dataToProcess.Count();
            
            try
            {
                _logger.LogInformation("Starting bulk copy of {Count} models of type {ModelType} to table {TableName}", 
                    count, typeof(T).Name, destinationTableName);

                dbConnection = LokiDbConnection.Create(appContext);
                
                transaction = dbConnection.BeginTransactionIfUsingInternalTransaction();
                _logger.LogInformation("Transaction initialized for bulk copy operation");
                
                var type = typeof(T);
                var mapping = appContext.ModelMappingCollection.GetMappingFor(type);
                var propertyNames = type.GetPublicPropertyNames();
                
                _logger.LogInformation("Mapped {PropertyCount} properties for bulk copy operation", propertyNames.Length);
    
                using var bulkCopyCommand = dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
    
                bulkCopyCommand.MapColumns(mapping, propertyNames);
                _logger.LogInformation("Beginning data transfer to server");
                await bulkCopyCommand.WriteToServerAsync(dataToProcess, propertyNames, destinationTableName);
    
                transaction.CommitIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
                transaction.DisposeIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
                dbConnection.DisposeIfUsingInternalTransaction();
                
                _logger.LogInformation("Successfully completed bulk copy of {Count} models to {TableName}", 
                    count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of {Count} models of type {ModelType} to {TableName}", 
                    count, typeof(T).Name, destinationTableName);
                transaction?.Rollback();
                throw;
            }
        }
    }
}
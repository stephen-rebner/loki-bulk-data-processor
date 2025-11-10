using System;
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Core.Context.Interfaces;
using Loki.BulkDataProcessor.Core.DataReaders;
using Loki.BulkDataProcessor.Utils.Validation;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using LokiBulkDataProcessor.Core.Interfaces;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {
        private readonly ICommandFactory _commandFactory;
        private readonly IAppContext _appContext;
        private readonly ILogger<BulkProcessor> _logger;

        public string ConnectionString
        {
            get => _appContext.ConnectionString;
            set
            {
                value.ThrowIfNullOrEmptyString(nameof(ConnectionString));
                _appContext.SetConnectionString(value);
            }
        }

        public int Timeout
        {
            get => _appContext.Timeout;
            set
            {
                value.ThrowIfLessThanZero(nameof(Timeout));
                _logger.LogInformation("Setting Timeout from {OldValue} to {NewValue}", _appContext.Timeout, value);
                _appContext.SetTimeout(value);
            }
        }

        public int BatchSize
        {
            get => _appContext.BatchSize;
            set
            {
                value.ThrowIfLessThanZero(nameof(BatchSize));
                _logger.LogInformation("Setting BatchSize from {OldValue} to {NewValue}", _appContext.BatchSize, value);
                _appContext.SetBatchSize(value);
            }
        }

        public IDbTransaction Transaction 
        { 
            get => _appContext.ExternalTransaction;
            set
            {
                value.ThrowIfNull(nameof(Transaction));
                _logger.LogInformation("Setting external transaction");
                _appContext.SetTransaction(value);
            }
        }
        
        public BulkProcessor(ICommandFactory commandFactory, IAppContext appContext, ILogger<BulkProcessor> logger = null)
        {
            _appContext = appContext;
            _commandFactory = commandFactory;
            _logger = logger ?? NullLogger<BulkProcessor>.Instance;
        }

        [Obsolete("Please use the ConnectionString property instead.")]        
        public IBulkProcessor WithConnectionString(string connectionString)
        {
            connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
            _appContext.SetConnectionString(connectionString);
            return this;
        }

        
        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));

            var count = dataToProcess is ICollection<T> collection ? collection.Count : dataToProcess.Count();
            _logger.LogInformation("Starting bulk copy of {Count} models to table {TableName}", count, destinationTableName);
        
            try
            {
                var command = _commandFactory.NewBulkCopyModelsCommand();
                await command.Execute(dataToProcess, destinationTableName);
            
                _logger.LogInformation("Successfully copied {Count} models to table {TableName}", count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of {Count} models to table {TableName}", count, destinationTableName);
                throw;
            }
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();

            _logger.LogInformation("Starting bulk copy of DataTable with {RowCount} rows to table {TableName}", 
                dataTable.Rows.Count, destinationTableName);
        
            try
            {
                var command = _commandFactory.NewBulkCopyDataTableCommand();
                await command.Execute(dataTable, destinationTableName);
            
                _logger.LogInformation("Successfully copied DataTable with {RowCount} rows to table {TableName}", 
                    dataTable.Rows.Count, destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy of DataTable to table {TableName}", destinationTableName);
                throw;
            }
        }

        public async Task SaveAsync(IDataReader dataReader, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
        
            _logger.LogInformation("Starting bulk copy from IDataReader to table {TableName}", destinationTableName);
        
            try
            {
                var command = _commandFactory.NewBulkCopyDataReaderCommand();
                await command.Execute(dataReader, destinationTableName);
            
                _logger.LogInformation("Successfully copied data from IDataReader to table {TableName}", destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy from IDataReader to table {TableName}", destinationTableName);
                throw;
            }
        }

        public async Task SaveAsync(Stream jsonStream)
        {
            jsonStream.ThrowIfNull(nameof(jsonStream));
        
            _logger.LogInformation("Starting bulk copy from JSON stream");
        
            try
            {
                using var jsonDocument = await JsonDocument.ParseAsync(jsonStream);
                using var dataReader = JsonDataReader.Create(jsonDocument, _logger);
                var destinationTableName = dataReader.TableName;
            
                _logger.LogInformation("Extracted table name {TableName} from JSON stream", destinationTableName);
                destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
    
                var command = _commandFactory.NewBulkCopyDataReaderCommand();
                await command.Execute(dataReader, destinationTableName);
            
                _logger.LogInformation("Successfully copied data from JSON stream to table {TableName}", destinationTableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk copy from JSON stream");
                throw;
            }
        }
    }
}
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Utils.Validation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {
        private readonly ICommandFactory _commandFactory;
        private readonly IAppContext _appContext;

        public int Timeout
        {
            get
            {
                return _appContext.Timeout;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(Timeout));
                _appContext.SetTimeout(value);
            }
        }

        public int BatchSize
        {
            get
            {
                return _appContext.BatchSize;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(BatchSize));
                _appContext.SetBatchSize(value);
            }
        }

        public string ConnectionString
        {
            get => _appContext.ConnectionString;
            set 
            {
                value.ThrowIfNullOrEmptyString(nameof(ConnectionString));
                _appContext.SetConnectionString(value);
            }
        }


        public BulkProcessor(ICommandFactory commandFactory, IAppContext appContext)
        {
            _appContext = appContext;
            _commandFactory = commandFactory;
        }


        [Obsolete]
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

            var command = _commandFactory.NewBulkCopyModelsCommand();

            await command.Execute(dataToProcess, destinationTableName);
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();

            var command = _commandFactory.NewBulkCopyDataTableCommand();

            await command.Execute(dataTable, destinationTableName);
        }

        public async Task UpdateAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();

            var createTempTableCommand = _commandFactory.NewCreateTempTableCommand();
            var bulkCopyToTempTableCommand = _commandFactory.NewBulkCopyDataTableToTempTable();
            var bulkUpdateCommand = _commandFactory.NewBulkUpdateDataTableCommand();
            var dropTempTableCommand = _commandFactory.NewDropTempTableCommand();

            //createTempTableCommand.Execute(destinationTableName);
            await bulkCopyToTempTableCommand.Execute(dataTable, destinationTableName);
            await bulkUpdateCommand.Execute(dataTable, destinationTableName);
            dropTempTableCommand.Execute();
        }
    }
}
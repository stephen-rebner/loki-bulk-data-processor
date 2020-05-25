using Loki.BulkDataProcessor.Commands.Factory;
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

        public BulkProcessor(ICommandFactory commandFactory, IAppContext appContext)
        {
            _commandFactory = commandFactory;
            _appContext = appContext;
        }

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

            var command = _commandFactory.NewBulkCopyModelsCommand(dataToProcess, destinationTableName);

            await command.Execute();
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();

            var command = _commandFactory.NewBulkCopyDataTableCommand(dataTable, destinationTableName);

            await command.Execute();
        }

        public Task UpdateAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();

            //var command = _commandFactory.NewBulkUpdateDataTableCommand(dataTable, destinationTableName);

            //await command.Execute();

            throw new NotImplementedException();
        }
    }
}
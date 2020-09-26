using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Utils.Validation;
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
            get =>  _appContext.Timeout;
            set
            {
                value.ThrowIfLessThanZero(nameof(Timeout));
                _appContext.SetTimeout(value);
            }
        }

        public int BatchSize
        {
            get =>  _appContext.BatchSize;
            set
            {
                value.ThrowIfLessThanZero(nameof(BatchSize));
                _appContext.SetBatchSize(value);
            }
        }

        public IDbTransaction Transaction 
        { 
            get => _appContext.Transaction;
            set
            {
                value.ThrowIfNull(nameof(Transaction));

                _appContext.SetTransaction(value);
            }
        }

        public BulkProcessor(ICommandFactory commandFactory, IAppContext appContext)
        {
            _appContext = appContext;
            _commandFactory = commandFactory;
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
    }
}
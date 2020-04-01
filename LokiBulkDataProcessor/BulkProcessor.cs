using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Utils.Validation;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _connectionString;
        private readonly ICommandFactory _commandFactory;
        private readonly IAppContext _appContext;
        private int _timeout;
        private int _batchSize;

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
            _appContext.SetConnectionString(connectionString);
            return this;
        }

        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));

            var command = _commandFactory.NewBulkCopyModelsCommand(
                _batchSize,
                _timeout,
                destinationTableName,
                _connectionString,
                dataToProcess);

            await command.Execute();
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataTable.ThrowIfNullOrHasZeroRows();
            
            var command = _commandFactory.NewBulkCopyDataTableCommand(
                _batchSize,
                _timeout,
                destinationTableName,
                _connectionString,
                dataTable);

            await command.Execute();
        }

    }
}
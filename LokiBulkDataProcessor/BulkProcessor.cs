using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Commands.Factory.Interface;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interface;
using Loki.BulkDataProcessor.Utils.Validation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {

        #region Private Variables

        private readonly IBulkCommandFactory _bulkCommandFactory;
        private readonly IModelDbContext _dbContext;
        private readonly IMediator _mediator;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _connectionString;

        private int _timeout;
        private int _batchSize;
        private DataTable _dataTableToProcess;
        private IEnumerable<object> _dataToProcess;

        #endregion


        #region Public Properties

        public int Timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(Timeout));
                _timeout = value;
            }
        }

        public int BatchSize
        {
            get
            {
                return _batchSize;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(BatchSize));
                _batchSize = value;
            }
        }

        #endregion


        #region Constructor

        public BulkProcessor(IMediator mediator, string connectionString)
        {
            _batchSize = DefaultConfigValues.BatchSize;
            _timeout = DefaultConfigValues.Timeout;
            _connectionString = connectionString;
            _mediator = mediator;
        }

        #endregion


        #region Public Methods

        [Obsolete("This method is depreciated, please use the fluent API")]
        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            //destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            //dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            //using var sqlConnection = _sqlConnection;
            //using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            //sqlConnection.Open();
            //SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            //using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            //await sqlBulkCopy.WriteToServerAsync(reader);
            //sqlBulkCopy.Close();
        }

        [Obsolete("This method is depreciated, please use the fluent API")]
        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            //dataTable.ThrowIfNullOrHasZeroRows();

            //using var sqlConnection = _sqlConnection;
            //using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            //sqlConnection.Open();
            //SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            //await sqlBulkCopy.WriteToServerAsync(dataTable);
            //sqlBulkCopy.Close();
        }

        public IBulkProcessor Update<T>(IEnumerable<T> dataToProcess) where T : class
        {
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            _dataToProcess = dataToProcess;

            return this;
        }

        public IBulkProcessor Table(string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));

            return this;
        }

        public async Task ExecuteUpdateWhere<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            // to think  about - what should happen if value passed is null?
            // Also does this make sense? maybe should be ExecuteUpdateWithJoin()?
            //var command = _bulkCommandFactory.NewCommand<BulkUpdateCommand>();
            //await command.ExecuteAsync();

            await _mediator.Send(new BulkUpdate.Command(_batchSize, _timeout, _connectionString, _dataToProcess));
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        #endregion

    }
}
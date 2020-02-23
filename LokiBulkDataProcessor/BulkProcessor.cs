using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Commands.Factory.Interface;
using Loki.BulkDataProcessor.Context.Interface;
using Loki.BulkDataProcessor.Utils.Validation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {

        #region Private Variables

        private readonly IBulkCommandFactory _bulkCommandFactory;
        private readonly IModelDbContext _dbContext;

        #endregion


        #region Public Properties

        public int Timeout
        {
            get
            {
                return _dbContext.Timeout;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(Timeout));
                _dbContext.UpdateTimeout(value);
            }
        }

        public int BatchSize
        {
            get
            {
                return _dbContext.BatchSize;
            }
            set
            {
                value.ThrowIfLessThanZero(nameof(BatchSize));
                _dbContext.UpdateBatchSize(value);
            }
        }

        #endregion


        #region Constructor

        public BulkProcessor(IBulkCommandFactory bulkCommandFactory, IModelDbContext dbContext)
        {
            _bulkCommandFactory = bulkCommandFactory;
            _dbContext = dbContext;
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

            _dbContext.AddModels(dataToProcess);

            return this;
        }

        public IBulkProcessor OnTable(string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));

            _dbContext.UpdateDestinationTableName(destinationTableName);

            return this;
        }

        public async Task ExecuteUpdateWhere<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            // to think  about - what should happen if value passed is null?
            // Also does this make sense? maybe should be ExecuteUpdateWithJoin()?
            var command = _bulkCommandFactory.NewCommand<BulkUpdateCommand>();
            await command.ExecuteAsync();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        #endregion

    }
}
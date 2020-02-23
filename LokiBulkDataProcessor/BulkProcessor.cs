using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using FastMember;
using Loki.BulkDataProcessor.Utils.Validation;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Data;
using System.Linq.Expressions;
using System;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Commands.Factory.Interface;
using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Context.Interface;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {

        #region Private Variables

        private readonly IBulkCommandFactory _bulkCommandFactory;
        private readonly IDbContext _dbContext;
        private int _timeout;
        private int _batchSize;

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
                _dbContext.UpdateTimeout(value);
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
                _dbContext.UpdateBatchSize(value);
            }
        }

        #endregion


        #region Constructor

        public BulkProcessor(IBulkCommandFactory bulkCommandFactory, IDbContext dbContext)
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

            //_dataToProcess = dataToProcess;
            
            return this;
        }

        public IBulkProcessor OnTable(string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));

            _dbContext.UpdateDestinationTableName(destinationTableName);

            return this;
        }

        public async Task ExecuteWhere<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            // to think  about - what should happen if value passed is null?
            var command = _bulkCommandFactory.NewCommand<BulkUpdateCommand>();
            await command.ExecuteAsync();
        }


        public async Task UpdateAsync<T>(
            IEnumerable<T> dataToProcess,
            string destinationTableName,
            Expression<Func<T, bool>> predicate) where T : class
        {
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            var command  = _bulkCommandFactory.NewCommand<BulkUpdateCommand>();
            await command.ExecuteAsync();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        #endregion


        #region Private Helper Methods

        private void SetUpSqlBulkCopier(SqlBulkCopy sqlBulkCopy, string destinationTableName)
        {
            sqlBulkCopy.BatchSize = _batchSize;
            sqlBulkCopy.BulkCopyTimeout = _timeout;
            sqlBulkCopy.DestinationTableName = destinationTableName;
        }

        

        #endregion

    }
}
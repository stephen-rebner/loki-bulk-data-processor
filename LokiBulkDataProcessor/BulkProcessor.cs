using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using FastMember;
using Loki.BulkDataProcessor.Utils.Validation;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Data;
using System.Linq.Expressions;
using System;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {

        #region Private Variables

        private readonly SqlConnection _sqlConnection;
        private readonly ITempTable _tempTable;
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

        public BulkProcessor(string connectionString)
        {
            connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
            
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
            _sqlConnection = new SqlConnection(connectionString);
        }

        #endregion


        #region Save Methods

        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            using var sqlConnection = _sqlConnection;
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            await sqlBulkCopy.WriteToServerAsync(reader);
            sqlBulkCopy.Close();
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            dataTable.ThrowIfNullOrHasZeroRows();

            using var sqlConnection = _sqlConnection;
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            await sqlBulkCopy.WriteToServerAsync(dataTable);
            sqlBulkCopy.Close();
        }

        #endregion


        #region Update Methods

        public async Task UpdateAsync<T>(
            IEnumerable<T> dataToProcess,
            string destinationTableName,
            Expression<Func<T, bool>> predicate) where T : class
        {
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            using var sqlConnection = _sqlConnection;
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);
            
            _tempTable.Create(typeof(T));

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy, $"#{ destinationTableName }");

            using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            await sqlBulkCopy.WriteToServerAsync(reader);

            sqlBulkCopy.Close();

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
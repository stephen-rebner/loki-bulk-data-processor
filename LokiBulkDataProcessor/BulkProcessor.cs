using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using FastMember;
using Loki.BulkDataProcessor.Utils.Validation;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Data;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {
        private readonly string _connectionString;
        private int _timeout;
        private int _batchSize;
        private string _destinationTableName;

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

        public string DestinationTableName
        {
            get
            {
                return _destinationTableName;
            }
            set
            {
                value.ThrowIfNullOrEmptyString(nameof(DestinationTableName));
                _destinationTableName = value;
            }
        }
   
        public BulkProcessor(string connectionString)
        {
            connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
            
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
            _connectionString = connectionString;
        }

        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess)
        {
            _destinationTableName.ThrowIfNullOrEmptyString(nameof(DestinationTableName));
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            using var sqlConnection = new SqlConnection(_connectionString);
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy);

            using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            await sqlBulkCopy.WriteToServerAsync(reader);
        }

        public async Task SaveAsync(DataTable dataTable)
        {
            dataTable.ThrowIfNullOrHasZeroRows();

            using var sqlConnection = new SqlConnection(_connectionString);
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy);

            await sqlBulkCopy.WriteToServerAsync(dataTable);

        }

        private void SetUpSqlBulkCopier(SqlBulkCopy sqlBulkCopy)
        {
            sqlBulkCopy.BatchSize = _batchSize;
            sqlBulkCopy.BulkCopyTimeout = _timeout;
            sqlBulkCopy.DestinationTableName = _destinationTableName;
        }
    }
}
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using FastMember;
using Loki.BulkDataProcessor.Utils.Validation;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Utils.Reflection;

namespace Loki.BulkDataProcessor
{
    public class BulkProcessor : IBulkProcessor
    {
        private string _connectionString;
        private int _timeout;
        private int _batchSize;

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
   
        public BulkProcessor(string connectionString)
        {
            _connectionString.ThrowIfNullOrEmptyString("The connection string must not be empty.", nameof(_connectionString));
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
            _connectionString = connectionString;
        }

        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName)
        {
            ValidateSourceParams(dataToProcess, destinationTableName);

            using var sqlConnection = new SqlConnection(_connectionString);

            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);
            sqlBulkCopy.BatchSize = _batchSize;
            sqlBulkCopy.BulkCopyTimeout = _timeout;
            sqlBulkCopy.DestinationTableName = destinationTableName;

            using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            await sqlBulkCopy.WriteToServerAsync(reader);
        }

        private void ValidateSourceParams<T>(IEnumerable<T> dataToProcess, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString("The destination table name is required.", nameof(destinationTableName));
            
            dataToProcess.ThrowIfArgumentIsNull($"The data collection to be proccessed is null. Please supply some data.", nameof(dataToProcess));
            dataToProcess.ThrowIfCollectionIsEmpty($"The data collection to be processed is empty. Please Supply some data.", nameof(dataToProcess));
        }

    }
}
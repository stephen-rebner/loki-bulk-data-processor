using FastMember;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Utils.Validation;

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
   

        public BulkProcessor()
        {
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
        }

        public Task<bool> DeleteAsync<T>(IEnumerable<T> data)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SaveAsync<T>(IEnumerable<T> dataToProcess, string connectionString, string destinationTableName)
        {
            ValidateSourceParams(dataToProcess, connectionString, destinationTableName);

            using(var sqlBulkCopy = new SqlBulkCopy(_connectionString))
            {
                sqlBulkCopy.BatchSize = _batchSize;
                sqlBulkCopy.BulkCopyTimeout = _timeout;
                sqlBulkCopy.DestinationTableName = destinationTableName;

                using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
                await sqlBulkCopy.WriteToServerAsync(reader);
            }

            return true;
        }

        public Task<IEnumerable<T>> UpdateAsync<T>(IEnumerable<T> data)
        {
            throw new NotImplementedException();
        }

        private void ValidateSourceParams<T>(IEnumerable<T> dataToProcess, string connectionString, string destinationTableName)
        {
            dataToProcess.ThrowIfArgumentIsNull($"The data collection to be proccessed is null. Please supply some data.", nameof(dataToProcess));
            dataToProcess.ThrowIfCollectionIsEmpty($"The data collection to be processed is empty. Please Supply some data.", nameof(dataToProcess));

            connectionString.ThrowIfArgumentIsNull("The connection string must not be null.", nameof(connectionString));
            connectionString.ThrowIfNullOrEmptyString("The connection string must not be empty.", nameof(connectionString));

            destinationTableName.ThrowIfArgumentIsNull("The destination table name must not be null.", nameof(destinationTableName));
            destinationTableName.ThrowIfNullOrEmptyString("The destination table name must not be empty.", nameof(destinationTableName));
        }

    }
}
using FastMember;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Repository
{
    public class BulkRepository : IBulkRepository
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
                value.ThrowExecptionIfLessThanZero(nameof(Timeout));
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
                value.ThrowExecptionIfGreaterThanOrEqualToZero(nameof(BatchSize));
                _batchSize = value;
            }
        }
   

        public BulkRepository()
        {
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
        }

        public IBulkRepository WithConnectionString(string connectionString)
        {
            ValidateConnectionString(connectionString);
            _connectionString = connectionString;
            return this;
        }

        public Task<bool> DeleteAsync<T>(IEnumerable<T> data)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName)
        {
            ValidateSourceParams(dataToProcess, destinationTableName);

            using(var sqlBulkCopy = new SqlBulkCopy(_connectionString))
            {
                sqlBulkCopy.BatchSize = _batchSize;
                sqlBulkCopy.BulkCopyTimeout = _timeout;
                sqlBulkCopy.DestinationTableName = destinationTableName;

                using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNamesAsArray());
                await sqlBulkCopy.WriteToServerAsync(reader);
            }

            return true;
        }

        public Task<IEnumerable<T>> UpdateAsync<T>(IEnumerable<T> data)
        {
            throw new NotImplementedException();
        }

        private void ValidateSourceParams<T>(IEnumerable<T> dataToProcess, string destinationTableName)
        {
            dataToProcess.ThrowExceptionIfArgumentIsNull($"The data collection to be proccessed is null. Please supply some data.", nameof(dataToProcess));
            dataToProcess.ThrowExceptionIfCollectionIsEmpty($"The data collection to be processed is empty. Please Supply some data.", nameof(dataToProcess));
            destinationTableName.ThrowExceptionIfArgumentIsNull("The connection string must not be null.", nameof(destinationTableName));
            destinationTableName.ThrowExceptionIfNullOrEmptyString("The connection string must not be empty.", nameof(destinationTableName));
        }

        private void ValidateConnectionString(string connectionString)
        {
            connectionString.ThrowExceptionIfArgumentIsNull("The connection string must not be null.", nameof(connectionString));
            connectionString.ThrowExceptionIfNullOrEmptyString("The connection string must not be empty.", nameof(connectionString));
        }
    }
}
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
        private readonly SqlConnection _sqlConnection;
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
            connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
            
            _timeout = DefaultConfigValues.Timeout;
            _batchSize = DefaultConfigValues.BatchSize;
            _sqlConnection = new SqlConnection(connectionString);
        }

        public async Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName)
        {
            destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
            dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));

            using var sqlConnection = _sqlConnection;
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            using var reader = ObjectReader.Create(dataToProcess, typeof(T).GetPublicPropertyNames());
            await sqlBulkCopy.WriteToServerAsync(reader);
        }

        public async Task SaveAsync(DataTable dataTable, string destinationTableName)
        {
            dataTable.ThrowIfNullOrHasZeroRows();

            using var sqlConnection = _sqlConnection;
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();
            SetUpSqlBulkCopier(sqlBulkCopy, destinationTableName);

            await sqlBulkCopy.WriteToServerAsync(dataTable);

        }

        private void SetUpSqlBulkCopier(SqlBulkCopy sqlBulkCopy, string destinationTableName)
        {
            sqlBulkCopy.BatchSize = _batchSize;
            sqlBulkCopy.BulkCopyTimeout = _timeout;
            sqlBulkCopy.DestinationTableName = destinationTableName;
        }
    }
}
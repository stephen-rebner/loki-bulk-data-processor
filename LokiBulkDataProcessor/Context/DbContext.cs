using Loki.BulkDataProcessor.Context.Interface;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Context
{
    public class DbContext : IDbContext
    {

        #region Private Members

        private string _connectionString;
        private SqlConnection _sqlConnection;

        #endregion


        #region Public Properties

        public int BatchSize { get; private set; }

        public int Timeout { get; private set; }

        public string DestinationTableName { get; private set; }

        #endregion


        #region Constructors

        public DbContext(string connectionString, int batchSize, int timeout)
        {
            BatchSize = batchSize;
            Timeout = timeout;
            _sqlConnection = new SqlConnection(_connectionString);
        }

        #endregion


        #region Interface Implementations

        public virtual void Dispose()
        {
            _sqlConnection.Close();
            _sqlConnection.Dispose();
        }

        public virtual SqlConnection OpenSqlConnection()
        {
            _sqlConnection.Open();
            return _sqlConnection;
        }

        public virtual void UpdateBatchSize(int batchSize)
        {
            BatchSize = batchSize;
        }

        public virtual void UpdateConnectionString(string connectionString)
        {
            _sqlConnection.ConnectionString = connectionString;
        }

        public virtual void UpdateDestinationTableName(string destinationTableName)
        {
            DestinationTableName = destinationTableName;
        }

        public virtual void UpdateTimeout(int timeout)
        {
            Timeout = timeout;
        }

        #endregion


    }
}

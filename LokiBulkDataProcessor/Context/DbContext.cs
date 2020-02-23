using Loki.BulkDataProcessor.Context.Interface;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Context
{
    public abstract class DbContext : IDbContext
    {

        #region Private Members

        private SqlConnection _sqlConnection;

        #endregion


        #region Public Properties

        public int BatchSize { get; private set; }

        public int Timeout { get; private set; }

        public string DestinationTableName { get; private set; }

        public virtual SqlConnection SqlConnection => _sqlConnection;

        #endregion


        #region Constructors

        public DbContext(string connectionString, int batchSize, int timeout)
        {
            BatchSize = batchSize;
            Timeout = timeout;
            _sqlConnection = new SqlConnection(connectionString);
            _sqlConnection.Open();
        }

        #endregion


        #region Interface Implementations

        public virtual void Dispose()
        {
            _sqlConnection.Close();
            _sqlConnection.Dispose();
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

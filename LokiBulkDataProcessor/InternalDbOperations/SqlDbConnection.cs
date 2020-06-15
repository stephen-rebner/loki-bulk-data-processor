using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class SqlDbConnection : IDbConnection
    {
        private SqlConnection _sqlConnection;

        public string ConnectionString 
        { 
            get => _sqlConnection.ConnectionString; 
            set => _sqlConnection.ConnectionString = value; 
        }

        public int ConnectionTimeout => _sqlConnection.ConnectionTimeout;

        public string Database => _sqlConnection.Database;

        public ConnectionState State => _sqlConnection.State;

        public IDbTransaction BeginTransaction()
        {
            return _sqlConnection.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return _sqlConnection.BeginTransaction(il);
        }

        public void ChangeDatabase(string databaseName)
        {
            _sqlConnection.ChangeDatabase(databaseName);
        }

        public void Close()
        {
            if (_sqlConnection.State != ConnectionState.Closed)
            {
                _sqlConnection.Close();
            }
        }

        public IDbCommand CreateCommand()
        {
            return _sqlConnection.CreateCommand();
        }

        public void Dispose()
        {
            _sqlConnection.Dispose();
        }

        public void Open()
        {
            _sqlConnection = new SqlConnection();
            _sqlConnection.Open();
        }
    }
}

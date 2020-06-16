using Loki.BulkDataProcessor.Context.Interfaces;
using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class SqlDbConnection : IDbConnection
    {
        private SqlConnection _sqlConnection;
        private readonly IAppContext _appContext;

        private SqlConnection SqlConnection
        {
            get
            {
                if(_sqlConnection == null)
                {
                    _sqlConnection = new SqlConnection(_appContext.ConnectionString);
                }

                return _sqlConnection;
            }
        }

        public SqlDbConnection(IAppContext appContext)
        {
            _appContext = appContext;
        }

        public string ConnectionString 
        { 
            get => SqlConnection.ConnectionString; 
            set => SqlConnection.ConnectionString = value; 
        }

        public int ConnectionTimeout => SqlConnection.ConnectionTimeout;

        public string Database => SqlConnection.Database;

        public ConnectionState State => SqlConnection.State;

        public IDbTransaction BeginTransaction()
        {
            return SqlConnection.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return SqlConnection.BeginTransaction(il);
        }

        public void ChangeDatabase(string databaseName)
        {
            SqlConnection.ChangeDatabase(databaseName);
        }

        public void Close()
        {
            if (SqlConnection.State != ConnectionState.Closed)
            {
                SqlConnection.Close();
            }
        }

        public IDbCommand CreateCommand()
        {
            return SqlConnection.CreateCommand();
        }

        public void Dispose()
        {
            SqlConnection.Dispose();
        }

        public void Open()
        {
            SqlConnection.Open();
        }
    }
}

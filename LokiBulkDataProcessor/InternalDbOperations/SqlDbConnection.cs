using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class SqlDbConnection : ISqlDbConnection
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
            // todo: create new method to do this
            return _appContext.Transaction ?? SqlConnection.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return SqlConnection.BeginTransaction(il);
        }

        public IDbTransaction BeginTransactionIfNotGivenByAppContext()
        {
            return _appContext.Transaction ?? SqlConnection.BeginTransaction();
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
            return new SqlCommand { CommandTimeout = _appContext.Timeout };
        }

        public IDbCommand CreateCommand(string commandText, SqlTransaction transaction)
        {
            return new SqlCommand(commandText, _sqlConnection, transaction)
            {
                CommandTimeout = _appContext.Timeout
            };
        }

        public IBulkCopyCommand CreateNewBulkCopyCommand(SqlTransaction transaction)
        {
            return new BulkCopyCommand(_sqlConnection, transaction, _appContext);
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

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
            return new SqlCommand 
            { 
                CommandTimeout = _appContext.Timeout,
                Connection = _sqlConnection
            };
        }

        public IDbCommand CreateCommand(string commandText, IDbTransaction transaction)
        {
            return new SqlCommand(commandText, _sqlConnection, (SqlTransaction)transaction)
            {
                CommandTimeout = _appContext.Timeout
            };
        }

        public IBulkCopyCommand CreateNewBulkCopyCommand(IDbTransaction transaction)
        {
            return new BulkCopyCommand(_sqlConnection, (SqlTransaction)transaction, _appContext);
        }

        public ICopyToTempTableCommand CreateNewCopyToTempTableCommand(IDbTransaction transaction)
        {
            return new CopyToTempTableCommand(this, _appContext, transaction);
        }

        public IQuery CreateQuery(IDbTransaction transaction)
        {
            var command = CreateCommand();

            command.Transaction = transaction;

            return new Query(command);
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

using Loki.BulkDataProcessor.Context.Interfaces;
using System;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands
{
    internal abstract class BaseBulkCommand : IDisposable
    { 
        private SqlConnection _sqlConnection;
        private SqlTransaction _transaction;
        protected readonly IAppContext AppContext;

        protected SqlBulkCopy SqlBulkCopy { get; set; }

        protected BaseBulkCommand(IAppContext appContext, string tableName)
        {
            AppContext = appContext;
            SetupSqlBulkCopy(tableName);
        }

        protected void AddDefaultMappings(string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                SqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(property, property));
            }
        }

        protected void ThrowInvalidOperationException(string errorMessage)
        {
            if(errorMessage.Equals(
                "The given ColumnMapping does not match up with any column in the source or destination.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The given column mappings doe not match up with any column in the destination table. Note the mappings are case sensitive.");
            }
            else
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        protected void CommitTransaction()
        {
            _transaction.Commit();
        }

        protected void RollbackTransaction()
        {
            _transaction.Rollback();
        }

        public void Dispose()
        {
            _sqlConnection.Close();
            _sqlConnection.Dispose();
            _transaction.Dispose();

            // Close on the SQL Bulk Copy method also dispose the instance. See below, line 789:
            https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/SqlClient/SqlBulkCopy.cs
            SqlBulkCopy.Close();
        }

        private void SetupSqlBulkCopy(string tableName)
        {
            _sqlConnection = new SqlConnection(AppContext.ConnectionString);
            _sqlConnection.Open();
            _transaction = _sqlConnection.BeginTransaction();

            SqlBulkCopy = new SqlBulkCopy(_sqlConnection, SqlBulkCopyOptions.CheckConstraints, _transaction)
            {
                BatchSize = AppContext.BatchSize,
                BulkCopyTimeout = AppContext.Timeout,
                DestinationTableName = tableName
            };
        }
    }
}

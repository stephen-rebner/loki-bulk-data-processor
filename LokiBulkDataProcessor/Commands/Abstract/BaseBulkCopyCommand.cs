using Loki.BulkDataProcessor.Context.Interfaces;
using System;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands.Abstract
{
    internal abstract class BaseBulkCopyCommand : IDisposable
    {
        private readonly IAppContext _appContext;
        private SqlConnection _sqlConnection;
        private SqlTransaction _transaction;
        protected SqlBulkCopy SqlBulkCopy { get; set; }

        protected BaseBulkCopyCommand(IAppContext appContext)
        {
            _appContext = appContext;
        }

        protected void StartTransaction(string tableName)
        {
            _sqlConnection = new SqlConnection(_appContext.ConnectionString);
            _sqlConnection.Open();
            _transaction = _sqlConnection.BeginTransaction();

            SqlBulkCopy = new SqlBulkCopy(_sqlConnection, SqlBulkCopyOptions.CheckConstraints, _transaction)
            {
                BatchSize = _appContext.BatchSize,
                BulkCopyTimeout = _appContext.Timeout,
                DestinationTableName = tableName
            };
        }

        protected void AddMappings(string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                SqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(property, property));
            }
        }

        protected void ThrowException(string errorMessage)
        {
            if (errorMessage.Equals(
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
    }
}

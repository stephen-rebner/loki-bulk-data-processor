using System;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands
{
    internal abstract class BaseBulkCommand : IDisposable
    { 
        private SqlConnection _sqlConnection;
        private SqlTransaction _transaction;
        protected SqlBulkCopy SqlBulkCopy { get; set; }

        protected BaseBulkCommand(
            int batchSize,
            int timeout,
            string tableName,
            string connectionString)
        {
            _sqlConnection = new SqlConnection(connectionString);
            _sqlConnection.Open();
            _transaction = _sqlConnection.BeginTransaction();

            SqlBulkCopy = new SqlBulkCopy(_sqlConnection, SqlBulkCopyOptions.CheckConstraints, _transaction)
            {
                BatchSize = batchSize,
                BulkCopyTimeout = timeout,
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
            if(errorMessage.Equals(
                "The given ColumnMapping does not match up with any column in the source or destination.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The given ColumnMapping does not match up with any column in the destination table. Note the mappings are case sensitive");
            }
            else
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        protected void SaveTransaction()
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

using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands.Abstract
{
    internal abstract class BaseBulkProcessorCommand : IDisposable
    { 
        protected string _destinationTableName;
        protected readonly IAppContext _appContext;
        protected SqlConnection _sqlConnection;
        protected SqlTransaction _transaction;
        protected SqlBulkCopy SqlBulkCopy { get; set; }

        protected BaseBulkProcessorCommand(IAppContext appContext, string destinationTableName)
        {
            _appContext = appContext;
            _destinationTableName = destinationTableName;
            SetupSqlBulkCopy();
        }

        protected void MapColumns(AbstractMapper mapping, string[] propertyNames)
        {
            if(mapping != null)
            {
                AddMappings(mapping);
            }
            else
            {
                AddDefaultMappings(propertyNames);
            }
        }

        protected void ThrowInvalidOperationException(string errorMessage)
        {
            if(errorMessage.Equals(
                "The given ColumnMapping does not match up with any column in the source or destination.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The given column mappings does not match up with any column in the destination table. Note the mappings are case sensitive.");
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

        private void SetupSqlBulkCopy()
        {
            _sqlConnection = new SqlConnection(_appContext.ConnectionString);
            _sqlConnection.Open();
            _transaction = _sqlConnection.BeginTransaction();

            SqlBulkCopy = new SqlBulkCopy(_sqlConnection, SqlBulkCopyOptions.CheckConstraints, _transaction)
            {
                BatchSize = _appContext.BatchSize,
                BulkCopyTimeout = _appContext.Timeout,
                DestinationTableName = _destinationTableName
            };
        }
        protected void AddMappings(AbstractMapper mapping)
        {
            foreach (var columnMapping in mapping.ColumnMappings)
            {
                SqlBulkCopy.ColumnMappings.Add(columnMapping.Key, columnMapping.Value);
            }
        }

        protected void AddDefaultMappings(string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                SqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(property, property));
            }
        }
    }
}

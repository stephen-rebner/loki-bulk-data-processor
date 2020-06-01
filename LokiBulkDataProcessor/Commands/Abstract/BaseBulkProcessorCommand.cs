using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Data.SqlClient;
using System.Linq;

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

        protected void MapColumns(AbstractMapping mapping, string[] propertyNames)
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
            //https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/SqlClient/SqlBulkCopy.cs
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
        protected void AddMappings(AbstractMapping mapping)
        {
            var nonPrimaryKeyMappings = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsPrimaryKey);

            foreach (var mappingMetaData in nonPrimaryKeyMappings)
            {
                SqlBulkCopy.ColumnMappings.Add(mappingMetaData.SourceColumn, mappingMetaData.DestinationColumn);
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

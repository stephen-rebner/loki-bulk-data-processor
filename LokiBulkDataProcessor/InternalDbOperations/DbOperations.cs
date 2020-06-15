using FastMember;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    /// <summary>
    /// A facade class implementing a number of different DB operations
    /// </summary>
    internal class DbOperations : IDbOperations
    {
        private SqlConnection _sqlConnection;
        private SqlTransaction _sqlTransaction;
        private SqlBulkCopy _sqlBulkCopy;

        private readonly IAppContext _appContext;
        private readonly ITempTable _tempTable;

        public DbOperations(IAppContext appContext, ITempTable tempTable)
        {
            _appContext = appContext;
            _tempTable = tempTable;
        }

        /// <summary>
        /// Adds the mappings for table columns / model object properties to be mapped.
        /// </summary>
        /// <param name="mapping">The mapping containing the mapping meta data</param>
        public void AddBulkCopyMappings(AbstractMapping mapping)
        {
            if(_sqlBulkCopy == null) throw new InvalidOperationException("You must create the SQL Bulk Copy class before you can add mappings");

            var nonPrimaryKeyMappings = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsPrimaryKey);

            foreach (var mappingMetaData in nonPrimaryKeyMappings)
            {
                _sqlBulkCopy.ColumnMappings.Add(mappingMetaData.SourceColumn, mappingMetaData.DestinationColumn);
            }
        }

        /// <summary>
        /// Adds default mappings for data table columns / model object properties to be mapped.
        /// </summary>
        /// <param name="propertyNames">The sources property names / data columnm names</param>
        public void AddDefaultBulkCopyMappings(string[] sourceNames)
        {
            if (_sqlBulkCopy == null) throw new InvalidOperationException("You must create the SQL Bulk Copy class before you can add mappings");

            foreach (var source in sourceNames)
            {
                _sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(source, source));
            }
        }

        public void BeginTransaction()
        {
            _sqlTransaction = _sqlConnection.BeginTransaction();
        }

        public void BulkCopyModelData<T>(IEnumerable<T> modelObjectsToCopy, ObjectReader reader) where T : class
        {
            _sqlBulkCopy.WriteToServerAsync(reader);
        }

        public void BulkCopyDataTable(DataTable dataTableToCopy, string destinationTableName)
        {
            using var sqlBulkCopy = CreateSqlBulkCopier(destinationTableName);
            sqlBulkCopy.WriteToServerAsync(dataTableToCopy);
        }

        public void CommitTransaction()
        {
            _sqlTransaction.Commit();
        }

        public void RollbackTransaction()
        {
            _sqlTransaction.Rollback();
        }

        /// <summary>
        /// Cleans up the unmanaged resources on the DbOperations class. 
        /// </summary>
        public void Dispose()
        {
            CloseAndDisposeSqlConnection();
            DisposeTransaction();
            CloseSqlBulkCopy();
        }

        public void OpenSqlConnection()
        {
            _sqlConnection = new SqlConnection(_appContext.ConnectionString);
            _sqlConnection.Open();
        }

        private SqlBulkCopy CreateSqlBulkCopier(string destinationTable)
        {
            _sqlConnection = new SqlConnection(_appContext.ConnectionString);
            _sqlTransaction = _sqlConnection.BeginTransaction();

            // todo: check what happens if you pass a null transaction below
            return new SqlBulkCopy(_sqlConnection, SqlBulkCopyOptions.CheckConstraints, _sqlTransaction)
            {
                BatchSize = _appContext.BatchSize,
                BulkCopyTimeout = _appContext.Timeout,
                DestinationTableName = destinationTable
            };
        }

        private void CloseSqlBulkCopy()
        {
            if (_sqlBulkCopy != null)
            {
                // Close on the SQL Bulk Copy method also dispose the instance. See below, line 789:
                //https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/SqlClient/SqlBulkCopy.cs
                _sqlBulkCopy.Close();
            }
        }

        private void CloseAndDisposeSqlConnection()
        {
            if(_sqlConnection != null)
            {
                _sqlConnection.Close();
                _sqlConnection.Dispose();
            }
        }

        private void DisposeTransaction()
        {
            if(_sqlTransaction != null)
            {
                _sqlTransaction.Dispose();
            }
        }
    }
}

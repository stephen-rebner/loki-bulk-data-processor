using FastMember;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class BulkCopyCommand : IBulkCopyCommand
    {
        private readonly SqlBulkCopy _sqlBulkCopy;
        private readonly IAppContext _appContext;

        internal BulkCopyCommand(SqlConnection sqlConnection, SqlTransaction transaction, IAppContext appContext)
        {
            _appContext = appContext;

            _sqlBulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.CheckConstraints, transaction)
            {
                BatchSize = _appContext.BatchSize,
                BulkCopyTimeout = _appContext.Timeout
            };
        }

        public async Task WriteToServerAsync(DataTable dataToCopy, string tableName)
        {
            try
            {
                _sqlBulkCopy.DestinationTableName = tableName;
                await _sqlBulkCopy.WriteToServerAsync(dataToCopy);
            }
            catch(Exception e)
            {
                ThrowInvalidOperationException(e.Message);
            }
        }

        public Task WriteToServerAsync(IDataReader dataReader, string tableName)
        {
            _sqlBulkCopy.DestinationTableName = tableName;
            return _sqlBulkCopy.WriteToServerAsync(dataReader);
        }

        public async Task WriteToServerAsync<T>(IEnumerable<T> dataToCopy, string[] propertyNames, string tableName) where T : class
        {
            try
            {
                using var reader = ObjectReader.Create(dataToCopy, propertyNames);

                _sqlBulkCopy.DestinationTableName = tableName;
                await _sqlBulkCopy.WriteToServerAsync(reader);
            }
            catch(Exception e)
            {
                ThrowInvalidOperationException(e.Message);
            }
        }

        public void MapColumns(AbstractMapping mapping, string[] propertyNames)
        {
            if (mapping != null)
            {
                AddMappings(mapping);
            }
            else
            {
                AddDefaultMappings(propertyNames);
            }
        }

        public void Dispose()
        {
            // Close on the SQL Bulk Copy method also dispose the instance. See below, line 789:
            //https://github.com/Microsoft/referencesource/blob/master/System.Data/System/Data/SqlClient/SqlBulkCopy.cs
            _sqlBulkCopy.Close();
        }

        private void AddMappings(AbstractMapping mapping)
        {
            foreach (var mappingMetaData in mapping.MappingInfo.MappingMetaDataCollection)
            {
                _sqlBulkCopy.ColumnMappings.Add(mappingMetaData.SourceColumn, mappingMetaData.DestinationColumn);
            }
        }

        private void AddDefaultMappings(string[] propertyNames)
        {
            foreach (var property in propertyNames)
            {
                _sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(property, property));
            }
        }

        private void ThrowInvalidOperationException(string errorMessage)
        {
            if (errorMessage.Equals(
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
    }
}

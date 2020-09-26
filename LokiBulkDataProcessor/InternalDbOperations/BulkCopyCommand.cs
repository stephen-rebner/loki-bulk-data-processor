﻿using FastMember;
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
    internal class BulkCopyCommand : IBulkCopyCommand
    {
        private readonly SqlBulkCopy _sqlBulkCopy;
        private readonly IAppContext _appContext;

        internal BulkCopyCommand(SqlConnection sqlConnection, SqlTransaction transaction, IAppContext appContext)
        {
            _appContext = appContext;

            _sqlBulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.KeepIdentity, transaction)
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

        public void MapPrimaryKey(AbstractMapping mapping)
        {
            var primaryKeyMapping = mapping.MappingInfo.MappingMetaDataCollection.FirstOrDefault(metaData => metaData.IsPrimaryKey);

            if(primaryKeyMapping == null) throw new InvalidOperationException("The Bulk Data Processor cannot map the primary key because it is not defined in your mapping");

            _sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(primaryKeyMapping.SourceColumn, primaryKeyMapping.DestinationColumn));
        }

        public void MapNonPrimaryKeyColumns(AbstractMapping mapping, IEnumerable<string> propertyNames)
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
            var nonIdentityColumns = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsPrimaryKey);

            foreach (var mappingMetaData in nonIdentityColumns)
            {
                _sqlBulkCopy.ColumnMappings.Add(mappingMetaData.SourceColumn, mappingMetaData.DestinationColumn);
            }
        }

        private void AddDefaultMappings(IEnumerable<string> propertyNames)
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

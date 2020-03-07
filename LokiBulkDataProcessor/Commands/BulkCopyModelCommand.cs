﻿using FastMember;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand<T> : IBulkCopyModelsCommand<T> where T : class
    {
        public IEnumerable<T> DataToCopy { get ; set ; }

        public int BatchSize { get; set; }

        public int Timeout { get; set; }

        public string TableName { get; set; }

        public string ConnectionString { get; set; }


        public BulkCopyModelsCommand(
            int batchSize, 
            int timeout, 
            string tableName, 
            string connectionString, 
            IEnumerable<T> dataToCopy)
        {
            BatchSize = batchSize;
            Timeout = timeout;
            TableName = tableName;
            DataToCopy = dataToCopy;
            ConnectionString = connectionString;
        }

        public async Task Execute()
        {
            using var sqlConnection = new SqlConnection(ConnectionString);
            using var sqlBulkCopy = new SqlBulkCopy(sqlConnection);

            sqlConnection.Open();

            sqlBulkCopy.BatchSize = BatchSize;
            sqlBulkCopy.BulkCopyTimeout = Timeout;
            sqlBulkCopy.DestinationTableName = TableName;

            var propertyNames = typeof(T).GetPublicPropertyNames();

            foreach (var property in propertyNames)
            {
                sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(property, property));
            }

            using var reader = ObjectReader.Create(DataToCopy, propertyNames);
            await sqlBulkCopy.WriteToServerAsync(reader);
        }
    }
}

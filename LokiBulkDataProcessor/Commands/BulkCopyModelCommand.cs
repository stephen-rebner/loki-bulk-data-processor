using FastMember;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand<T> : BaseBulkCommand, IBulkCopyModelsCommand<T> where T : class
    {
        public IEnumerable<T> DataToCopy { get ; set ; }

        public BulkCopyModelsCommand(
            int batchSize, 
            int timeout, 
            string tableName, 
            string connectionString, 
            IEnumerable<T> dataToCopy) : base(batchSize, timeout, tableName, connectionString)
        {
            DataToCopy = dataToCopy;
        }

        public async Task Execute()
        {
            var propertyNames = typeof(T).GetPublicPropertyNames();

            AddMappings(propertyNames);

            try
            {
                using var reader = ObjectReader.Create(DataToCopy, propertyNames);
                await SqlBulkCopy.WriteToServerAsync(reader);
                SaveTransaction(); 
            }
            catch(Exception e)
            {
                RollbackTransaction();
                ThrowException(e.Message);
            }
            finally
            {
                Dispose();
            }
        }
    }
}

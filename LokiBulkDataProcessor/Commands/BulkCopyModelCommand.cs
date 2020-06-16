using FastMember;
using Loki.BulkDataProcessor.Commands.Abstract;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand : IBulkModelsCommand
    {
        private readonly IAppContext _appContext;

        public BulkCopyModelsCommand(IAppContext appContext)
        {
            _appContext = appContext;
        }

        public async Task Execute<T>(IEnumerable<T> dataToProcess, string destinationTableName) where T : class
        {
            try
            {
                var propertyNames = typeof(T).GetPublicPropertyNames();
                var mapping = _appContext.ModelMappingCollection.GetMappingFor(typeof(T));

                //MapColumns(mapping, propertyNames);
                //using var reader = ObjectReader.Create(_dataToCopy, propertyNames);
                //await SqlBulkCopy.WriteToServerAsync(reader);
                //CommitTransaction();
            }
            catch (Exception e)
            {
                //RollbackTransaction();
                //ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                //Dispose();
            }
        }
    }
}
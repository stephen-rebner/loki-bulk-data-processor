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
    internal class BulkCopyModelsCommand<T> : BaseBulkProcessorCommand, IBulkProcessorModelsCommand<T> where T : class
    {
        public IEnumerable<T> DataToCopy { get; set; }

        public BulkCopyModelsCommand(IEnumerable<T> dataToCopy, string tableName, IAppContext appContext) 
            : base(appContext, tableName)
        {
            DataToCopy = dataToCopy;
        }

        public async Task Execute()
        {
            try
            {
                var propertyNames = typeof(T).GetPublicPropertyNames();
                var mapping = _appContext.ModelMappingCollection.GetMappingFor(typeof(T));

                MapColumns(mapping, propertyNames);
                using var reader = ObjectReader.Create(DataToCopy, propertyNames);
                await SqlBulkCopy.WriteToServerAsync(reader);
                CommitTransaction();
            }
            catch (Exception e)
            {
                RollbackTransaction();
                ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                Dispose();
            }
        }
    }
}

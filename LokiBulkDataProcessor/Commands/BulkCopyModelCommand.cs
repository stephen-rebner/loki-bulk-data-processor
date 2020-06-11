using FastMember;
using Loki.BulkDataProcessor.Commands.Abstract;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkCopyModelsCommand<T> : IBulkProcessorCommand where T : class
    {
        private readonly IDbOperations _dbOperations;
        private readonly string _destinationTableName;
        private readonly IAppContext _appContext;
        private readonly IEnumerable<T> _dataToCopy;

        public BulkCopyModelsCommand(IEnumerable<T> dataToCopy, string destinationTableName, IAppContext appContext, IDbOperations dbOperations)
        {
            _dataToCopy = dataToCopy;
            _dbOperations = dbOperations;
            _destinationTableName = destinationTableName;
            _appContext = appContext;
        }

        public async Task Execute()
        {
            try
            {
                var propertyNames = typeof(T).GetPublicPropertyNames();
                var mapping = _appContext.ModelMappingCollection.GetMappingFor(typeof(T));

                _dbOperations.OpenSqlConnection();
                _dbOperations.BeginTransaction();

                _dbOperations.CreateSqlBulkCopier(_destinationTableName);

                if (mapping != null)
                {
                    _dbOperations.AddBulkCopyMappings(mapping);
                }
                else
                {
                    _dbOperations.AddDefaultBulkCopyMappings(propertyNames);
                }

                using var reader = ObjectReader.Create(_dataToCopy, propertyNames);

                await _dbOperations.BulkCopyModelData(_dataToCopy, reader);
                
                _dbOperations.CommitTransaction();
            }
            catch (Exception e)
            {
                _dbOperations.RollbackTransaction();
                // todo: figure out best place to handle exception below
                //ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                _dbOperations.Dispose();
            }
        }
    }
}

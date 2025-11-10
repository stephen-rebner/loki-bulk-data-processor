using Loki.BulkDataProcessor.Core.Context.Interfaces;
using Loki.BulkDataProcessor.Core.DefaultValues;
using Loki.BulkDataProcessor.Core.Mappings.Interfaces;
using System.Data;
using System.Diagnostics;

namespace Loki.BulkDataProcessor.Core.Context
{
    internal sealed class AppContext : IAppContext
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string ConnectionString { get; private set; }

        public int BatchSize { get; private set; }

        public int Timeout { get; private set; }

        public IDbTransaction? ExternalTransaction { get; private set; }

        public bool IsUsingExternalTransaction => ExternalTransaction != null;

        public IModelMappingCollection ModelMappingCollection { get; }

        public IDataMappingCollection DataMappingCollection { get; }

        public AppContext(string connectionString, IModelMappingCollection mappingCollection, IDataMappingCollection dataMappingCollection)
        {
            ConnectionString = connectionString;
            ModelMappingCollection = mappingCollection;
            DataMappingCollection = dataMappingCollection;
            BatchSize = DefaultConfigValues.BatchSize;
            Timeout = DefaultConfigValues.Timeout;
        }

        public void SetConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public void SetBatchSize(int batchSize)
        {
            BatchSize = batchSize;
        }

        public void SetTimeout(int timeout)
        {
            Timeout = timeout;
        }

        public void SetTransaction(IDbTransaction transaction)
        {
            ExternalTransaction = transaction;
        }
    }
}

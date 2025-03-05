using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Data;

namespace Loki.BulkDataProcessor.Context.Interfaces
{
    public interface IAppContext
    {
        public string ConnectionString { get; }

        public int BatchSize { get; }

        public int Timeout { get; }

        public IDbTransaction ExternalTransaction { get;  }

        public bool IsUsingExternalTransaction { get; }

        public IModelMappingCollection ModelMappingCollection { get; }

        IDataMappingCollection DataMappingCollection { get; }

        void SetConnectionString(string connectionString);

        void SetBatchSize(int batchSize);

        void SetTimeout(int timeout);

        void SetTransaction(IDbTransaction transaction);

    }
}

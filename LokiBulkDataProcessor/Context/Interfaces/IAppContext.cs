using Loki.BulkDataProcessor.Mappings.Interfaces;

namespace Loki.BulkDataProcessor.Context.Interfaces
{
    public interface IAppContext
    {
        public string ConnectionString { get; }
        public int BatchSize { get; }
        public int Timeout { get; }
        public IMappingCollection MappingCollection { get; }
        void SetConnectionString(string connectionString);
        void SetBatchSize(int batchSize);
        void SetTimeout(int timeout);
    }
}

using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using System;

namespace Loki.BulkDataProcessor.Context
{
    internal class AppContext : IAppContext
    {
        public string ConnectionString { get; }

        public int BatchSize { get; private set; }

        public int Timeout { get; private set; }

        public IMappingCollection MappingCollection { get; }

        public AppContext(string connectionString, IMappingCollection mappingCollection)
        {
            ConnectionString = connectionString;
            MappingCollection = mappingCollection;
            BatchSize = DefaultConfigValues.BatchSize;
            Timeout = DefaultConfigValues.Timeout;
        }

        public void SetConnectionString(string connectionString)
        {
            throw new NotImplementedException();
        }

        public void SetBatchSize(int batchSize)
        {
            BatchSize = batchSize;
        }

        public void SetTimeout(int timeout)
        {
            Timeout = timeout;
        }
    }
}

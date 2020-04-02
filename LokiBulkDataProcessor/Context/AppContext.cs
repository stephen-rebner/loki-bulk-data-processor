using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.DefaultValues;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Utils.Validation;
using System;
using System.Diagnostics;

namespace Loki.BulkDataProcessor.Context
{
    internal class AppContext : IAppContext
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string ConnectionString { get; private set; }

        public int BatchSize { get; private set; }

        public int Timeout { get; private set; }

        public IModelMappingCollection ModelMappingCollection { get; }
        public IDataTableMappingCollection DataTableMappingCollection { get; }


        public AppContext(string connectionString, IModelMappingCollection mappingCollection, IDataTableMappingCollection dataTableMappingCollection)
        {
            ConnectionString = connectionString;
            ModelMappingCollection = mappingCollection;
            DataTableMappingCollection = dataTableMappingCollection;
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
    }
}

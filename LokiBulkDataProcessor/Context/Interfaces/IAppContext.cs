using Loki.BulkDataProcessor.Mappings.Interfaces;
using System.Data;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Context.Interfaces
{
    public interface IAppContext
    {
        public int BatchSize { get; }
        
        public string ConnectionString { get; }
                        
        SqlBulkCopyOptions SqlBulkCopyOptions { get; }

        public int Timeout { get; }

        public IDbTransaction ExternalTransaction { get; }

        public bool IsUsingExternalTransaction { get; }

        public IModelMappingCollection ModelMappingCollection { get; }

        IDataTableMappingCollection DataTableMappingCollection { get; }

        void SetConnectionString(string connectionString);

        void SetBatchSize(int batchSize);

        void SetSqlBulkCopyOptions(SqlBulkCopyOptions sqlBulkCopyOptions);

        void SetTimeout(int timeout);

        void SetTransaction(IDbTransaction transaction);
    }
}
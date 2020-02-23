using Loki.BulkDataProcessor.Context.Interface;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Context
{
    public class ModelDbContext : DbContext, IModelDbContext
    {
        public IEnumerable<object> Models { get; private set; }

        public ModelDbContext(string connectionString, int batchSize, int timeout) 
            : base(connectionString, batchSize, timeout) { }

        public void AddModels<T>(IEnumerable<T> models) where T : class
        {
            Models = models;
        }
    }
}

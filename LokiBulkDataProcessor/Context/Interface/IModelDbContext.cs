using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Context.Interface
{
    public interface IModelDbContext : IDbContext
    {
        IEnumerable<object> Models { get; }

        void AddModels<T>(IEnumerable<T> models) where T : class;
    }
}

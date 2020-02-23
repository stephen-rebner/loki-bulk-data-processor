using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    public interface IBulkProcessor : IDisposable
    {
        int Timeout { get; set; }

        int BatchSize { get; set; }

        Task SaveAsync<T>(
            IEnumerable<T> dataToProcess, 
            string destinationTableName) where T : class;

        Task SaveAsync(DataTable dataTable, string destinationTableName);

        IBulkProcessor Update<T>(IEnumerable<T> dataToProcess) where T : class;

        IBulkProcessor OnTable(string destinationTableName);

        Task ExecuteWhere<T>(Expression<Func<T, bool>> whereExpression) where T : class;
    }
}

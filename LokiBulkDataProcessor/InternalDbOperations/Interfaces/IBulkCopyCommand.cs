using Loki.BulkDataProcessor.Core.Mappings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface IBulkCopyCommand : IDisposable
    {
        Task WriteToServerAsync<T>(IEnumerable<T> dataToCopy, string[] propertyNames, string tableName) where T : class;

        Task WriteToServerAsync(DataTable dataToCopy, string tableName);
        
        Task WriteToServerAsync(IDataReader dataReader, string tableName);

        void MapColumns(AbstractMapping mapping, string[] propertyNames);
    }
}

using FastMember;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface IDbOperations : IDisposable
    {
        void AddBulkCopyMappings(AbstractMapping mapping);

        void AddDefaultBulkCopyMappings(string[] propertyNames);

        void BeginTransaction();

        void CommitTransaction();
        
        void RollbackTransaction();

        Task BulkCopyModelData<T>(IEnumerable<T> modelObjectsToCopy, ObjectReader reader) where T : class;

        Task BulkCopyDataTable(DataTable dataTableToCopy);

        void CreateSqlBulkCopier(string destinationTable);

        void OpenSqlConnection();
    }
}

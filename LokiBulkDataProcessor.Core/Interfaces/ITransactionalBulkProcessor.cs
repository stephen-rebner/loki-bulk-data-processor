using System.Data;

namespace LokiBulkDataProcessor.Core.Interfaces
{
    /// <summary>
    /// Interface for transactional relational database bulk processors
    /// Combines relational operations with transaction support
    /// </summary>
    public interface ITransactionalBulkProcessor : IRelationalBulkProcessor
    {
        IDbTransaction Transaction { get; set; }
    }
}

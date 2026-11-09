using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor
{
    /// <summary>
    /// Bulk-copies data into one database through one provider. Settings belong to this instance only.
    /// Instances are cheap and NOT thread-safe: use one per unit of work (for example one per DI scope or per
    /// <see cref="IBulkProcessorFactory.Create"/> call).
    /// </summary>
    public interface IBulkProcessor
    {
        /// <summary>The provider this processor writes through (for example "SqlServer").</summary>
        string ProviderName { get; }

        /// <summary>Operation timeout in seconds (default 30; 0 means no limit). Must not be negative.</summary>
        int Timeout { get; set; }

        /// <summary>Rows per batch (default 5000; 0 means a single batch). Must not be negative.</summary>
        int BatchSize { get; set; }

        /// <summary>
        /// A caller-owned transaction to write inside. Loki never commits, rolls back, disposes or closes it or its
        /// connection, also when a save fails: the caller decides. Set <c>null</c> to clear it; Loki then uses its own
        /// transaction per save. Setting a transaction of another driver throws <see cref="System.ArgumentException"/>.
        /// </summary>
        IDbTransaction Transaction { get; set; }

        /// <summary>Copies <paramref name="dataToProcess"/> into <paramref name="destinationTableName"/>, using the
        /// <c>ModelMapping&lt;T&gt;</c> for <typeparamref name="T"/> if one is registered, else property names 1:1.</summary>
        Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName, CancellationToken cancellationToken = default) where T : class;

        /// <summary>Copies <paramref name="dataTable"/> into <paramref name="destinationTableName"/>. The <c>DataMapping</c>
        /// whose SourceTableName equals <c>dataTable.TableName</c> is used; if the table has no name, the destination table name is the key.</summary>
        Task SaveAsync(DataTable dataTable, string destinationTableName, CancellationToken cancellationToken = default);

        /// <summary>Copies <paramref name="dataReader"/> into <paramref name="destinationTableName"/>. The <c>DataMapping</c>
        /// whose SourceTableName equals <paramref name="destinationTableName"/> is used. The reader is not disposed.</summary>
        Task SaveAsync(IDataReader dataReader, string destinationTableName, CancellationToken cancellationToken = default);

        /// <summary>Copies <paramref name="dataReader"/> into <paramref name="destinationTableName"/>. The reader is not disposed.</summary>
        /// <param name="dataReader">The rows to copy.</param>
        /// <param name="destinationTableName">The destination table.</param>
        /// <param name="sourceName">Key used to find a <see cref="Mappings.DataMapping"/> (its SourceTableName). When blank,
        /// the destination table name is the key.</param>
        /// <param name="cancellationToken">Cancels the copy.</param>
        Task SaveAsync(IDataReader dataReader, string destinationTableName, string sourceName, CancellationToken cancellationToken = default);

        /// <summary>Copies a JSON document (<c>tableName</c>, <c>columns</c>, <c>data</c>) into the table it names. The
        /// <c>DataMapping</c> whose SourceTableName equals that table name is used. The payload chooses the destination
        /// table, so only pass payloads you trust to write to that table.</summary>
        Task SaveAsync(Stream jsonStream, CancellationToken cancellationToken = default);
    }
}

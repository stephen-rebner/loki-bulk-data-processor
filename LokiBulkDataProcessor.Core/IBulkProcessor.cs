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
    /// <remarks>
    /// Failures surface as <see cref="System.InvalidOperationException"/> with a fixed, data-free message (destination
    /// table and, when available, the database error number); the driver's exception is the
    /// <see cref="System.Exception.InnerException"/>. Driver messages can contain row values, so log them only under
    /// your own data-handling policy. SQL Server: the copy runs with CheckConstraints and KeepNulls but without
    /// FireTriggers, so INSERT triggers on the destination table do not run.
    /// </remarks>
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
        /// <remarks>
        /// Some server errors abort (doom) the transaction on the server even though Loki leaves it alone, for example
        /// a SQL Server error with XACT_ABORT on, or a severe error. After a failed save, check whether the transaction
        /// is still usable and roll it back if not; do not assume you can keep writing in it.
        /// </remarks>
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
        /// table and mapping, so only pass payloads you trust to write to that table. For payloads from callers you do
        /// not fully trust, use <see cref="SaveAsync(Stream, string, CancellationToken)"/>, which writes to the table you
        /// choose. The payload is buffered in memory; limit it with <c>LokiBulkDataProcessorOptions.MaxJsonPayloadBytes</c>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="jsonStream"/> is null (thrown synchronously).</exception>
        /// <exception cref="System.Text.Json.JsonException">The payload is not valid JSON, or nests deeper than 64 levels.</exception>
        /// <exception cref="InvalidDataException">The payload does not follow Loki's JSON schema, or is larger than
        /// <c>MaxJsonPayloadBytes</c>.</exception>
        Task SaveAsync(Stream jsonStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies a JSON document (<c>tableName</c>, <c>columns</c>, <c>data</c>) into <paramref name="destinationTableName"/>.
        /// The payload's <c>tableName</c> is validated as part of the schema but ignored for routing and mapping: the
        /// destination and the <c>DataMapping</c> are chosen by the caller, not by the payload.
        /// </summary>
        /// <remarks>
        /// Mapping rule (#16): JSON has no separate source name, so the mapping key is <paramref name="destinationTableName"/>:
        /// the <c>DataMapping</c> whose SourceTableName equals it is used, otherwise columns map 1:1 by name. Set
        /// <c>LokiBulkDataProcessorOptions.RequireDataMappingForJson</c> to reject saves with no mapping, so the payload
        /// cannot choose columns. The payload is buffered in memory; limit it with
        /// <c>LokiBulkDataProcessorOptions.MaxJsonPayloadBytes</c>.
        /// </remarks>
        /// <param name="jsonStream">The JSON document. It is read to the end but not disposed.</param>
        /// <param name="destinationTableName">The destination table, and the DataMapping key.</param>
        /// <param name="cancellationToken">Cancels the read and the copy.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="jsonStream"/> is null (thrown synchronously).</exception>
        /// <exception cref="System.ArgumentException"><paramref name="destinationTableName"/> is blank (thrown synchronously).</exception>
        /// <exception cref="System.Text.Json.JsonException">The payload is not valid JSON, or nests deeper than 64 levels.</exception>
        /// <exception cref="InvalidDataException">The payload does not follow Loki's JSON schema, or is larger than
        /// <c>MaxJsonPayloadBytes</c>.</exception>
        /// <exception cref="System.InvalidOperationException"><c>RequireDataMappingForJson</c> is on and no DataMapping
        /// matches (thrown before connecting), or the copy failed.</exception>
        Task SaveAsync(Stream jsonStream, string destinationTableName, CancellationToken cancellationToken = default);
    }
}

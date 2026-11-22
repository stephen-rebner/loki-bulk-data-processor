using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// <see cref="OracleWriteMode.DirectPath"/>: one <c>OracleBulkCopy</c> direct-path load per write, on the context's connection. The
/// load joins the connection's active transaction implicitly (OracleBulkCopy has no transaction parameter), and the writer never
/// uses <c>UseInternalTransaction</c> or <c>EnforceIndexChecks</c> (the spike showed the latter hanging past its timeout). A failed
/// load leaves none of its rows behind. The writer never commits, rolls back or disposes the connection or transaction.
/// </summary>
/// <remarks>
/// ODP.NET has no asynchronous direct-path API: <c>WriteToServer</c> blocks the calling thread, and the token is checked only up to
/// the moment the load starts.
/// </remarks>
internal sealed class DirectPathOracleWriter(OracleConnection connection, BulkWriteOptions options,
    IOracleIdentifierResolver resolver, ILogger logger) : IBulkWriter
{
    /// <summary>Never <c>UseInternalTransaction</c> (it would commit per batch, and fails inside a caller's transaction) or
    /// <c>EnforceIndexChecks</c>.</summary>
    internal const OracleBulkCopyOptions CopyOptions = OracleBulkCopyOptions.Default;

    public async Task WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        OracleResolvedTarget resolved = await resolver.ResolveAsync(connection, target, options.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<int> ordinals = SourceOrdinals(source, resolved);
        cancellationToken.ThrowIfCancellationRequested();   // last cancellation point: WriteToServer is synchronous
        using OracleBulkCopy bulkCopy = CreateBulkCopy(resolved, ordinals);
        bulkCopy.WriteToServer(source);
        logger.LogDebug("Direct-path load of {ColumnCount} columns completed", ordinals.Count);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Each mapped source column's ordinal, in target order. Fails before the load starts.</summary>
    internal static IReadOnlyList<int> SourceOrdinals(IDataReader source, OracleResolvedTarget resolved)
    {
        SourceColumns.ThrowIfEmpty(resolved);
        return resolved.Columns.Select(column => SourceColumns.Ordinal(source, column.SourceColumn)).ToArray();
    }

    /// <summary>
    /// The configured bulk copy. Columns are mapped by source ordinal (Loki already matched the names) to the resolver's quoted
    /// destination names; the table and schema are the resolver's quoted names too.
    /// </summary>
    internal OracleBulkCopy CreateBulkCopy(OracleResolvedTarget resolved, IReadOnlyList<int> ordinals)
    {
        var bulkCopy = new OracleBulkCopy(connection, CopyOptions)
        {
            DestinationTableName = resolved.TableSql,
            BatchSize = Math.Max(0, options.BatchSize),
            BulkCopyTimeout = Math.Max(0, options.TimeoutSeconds)
        };
        if (resolved.SchemaSql is not null)
        {
            bulkCopy.DestinationSchemaName = resolved.SchemaSql;
        }

        for (int i = 0; i < ordinals.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new OracleBulkCopyColumnMapping(ordinals[i], resolved.Columns[i].DestinationSql));
        }

        return bulkCopy;
    }
}

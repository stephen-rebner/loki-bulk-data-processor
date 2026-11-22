using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// <see cref="OracleWriteMode.DirectPath"/> (staging tables only): one <c>OracleBulkCopy</c> direct-path load per write, on the context's
/// connection, followed by <see cref="UnusableIndexCheck"/>. The load joins the connection's active transaction implicitly (OracleBulkCopy
/// has no transaction parameter), and the writer never uses <c>UseInternalTransaction</c> or <c>EnforceIndexChecks</c> (the spike showed
/// the latter hanging past its timeout). A failed load leaves none of its rows behind. The writer never commits, rolls back or disposes
/// the connection or transaction.
/// </summary>
/// <remarks>
/// <para>
/// INVARIANT: the pipeline (<c>ConnectionScope</c>) always gives the writer a connection with an active transaction, Loki's own or the
/// caller's. Without one, <c>WriteToServer</c> would commit the load itself (autocommit), and the unusable-index check below could no
/// longer stop a load with duplicate keys from being committed.
/// </para>
/// <para>
/// ODP.NET has no asynchronous direct-path API: <c>WriteToServer</c> blocks the calling thread, and the token is checked only up to
/// the moment the load starts (an approved decision: connections are never closed to interrupt a load).
/// </para>
/// </remarks>
internal sealed class DirectPathOracleWriter(OracleConnection connection, BulkWriteOptions options,
    IOracleIdentifierResolver resolver, OracleTypeMap typeMap, string providerName, ILogger logger) : IBulkWriter
{
    /// <summary>Never <c>UseInternalTransaction</c> (it would commit per batch, and fails inside a caller's transaction) or
    /// <c>EnforceIndexChecks</c>.</summary>
    internal const OracleBulkCopyOptions CopyOptions = OracleBulkCopyOptions.Default;

    public async Task WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Direct-path load (staging tables only) to {TableName} via {Provider}",
            BulkCopyErrorMessages.Sanitize(target.TableName), providerName);
        OracleResolvedTarget resolved = await resolver.ResolveAsync(connection, target, options.TimeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        DirectPathLoad load = Plan(source, resolved, options, typeMap);
        cancellationToken.ThrowIfCancellationRequested();   // last cancellation point: WriteToServer is synchronous
        Load(source, load);
        await UnusableIndexCheck.ThrowIfAnyUnusableAsync(connection, resolved, target.TableName, options.TimeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        logger.LogDebug("Direct-path load of {ColumnCount} columns completed", load.Columns.Count);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The load's configuration: each mapped source column's ordinal (Loki already matched the names, so columns are mapped by ordinal)
    /// with the resolver's quoted destination column, the quoted table and schema, BatchSize (0 means
    /// <see cref="ArrayBindOracleWriter.DefaultChunkSize"/>, so memory stays bounded as with array bind) and Timeout. Every source column's
    /// type is checked against <paramref name="typeMap"/> (validation only: ODP.NET converts the values itself), so an unsupported type fails
    /// before the load with the same marked <see cref="NotSupportedException"/> as array bind.
    /// </summary>
    internal static DirectPathLoad Plan(IDataReader source, OracleResolvedTarget resolved, BulkWriteOptions options, OracleTypeMap typeMap)
    {
        SourceColumns.ThrowIfEmpty(resolved);
        DirectPathColumn[] columns = resolved.Columns.Select(column => PlanColumn(source, column, typeMap)).ToArray();
        return new DirectPathLoad(resolved.SchemaSql, resolved.TableSql, BatchSize(options.BatchSize), Math.Max(0, options.TimeoutSeconds), columns);
    }

    /// <summary>BatchSize 0 (or less) means <see cref="ArrayBindOracleWriter.DefaultChunkSize"/> rows per batch.</summary>
    internal static int BatchSize(int batchSize) => batchSize <= 0 ? ArrayBindOracleWriter.DefaultChunkSize : batchSize;

    private static DirectPathColumn PlanColumn(IDataReader source, OracleResolvedColumn column, OracleTypeMap typeMap)
    {
        int ordinal = SourceColumns.Ordinal(source, column.SourceColumn);
        Type fieldType = source.GetFieldType(ordinal);
        typeMap.For(fieldType, column.SourceColumn);
        ThrowIfNotSupportedByDirectPath(fieldType, column.SourceColumn);
        return new DirectPathColumn(ordinal, column.DestinationSql);
    }

    /// <summary>
    /// Types the array-bind writer converts but ODP.NET's direct path cannot (verified on Oracle 23.26 in the step 8 review:
    /// <c>DateOnly</c> fails with InvalidCastException, <c>TimeOnly</c> with ArgumentException). Rejected before the load.
    /// </summary>
    internal static void ThrowIfNotSupportedByDirectPath(Type fieldType, string sourceColumn)
    {
        Type type = fieldType is null ? null : Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        if (type == typeof(DateOnly) || type == typeof(TimeOnly))
        {
            throw OracleValidation.NotSupported(
                $"The source column '{BulkCopyErrorMessages.Sanitize(sourceColumn)}' has type {type.Name}, which Oracle direct-path loads do not " +
                "support; convert it to DateTime or TimeSpan, or use OracleWriteMode.ArrayBind.");
        }
    }

    /// <summary>Runs the load synchronously; the bulk copy (not the connection) is disposed afterwards.</summary>
    private void Load(IDataReader source, DirectPathLoad load)
    {
        using OracleBulkCopy bulkCopy = CreateBulkCopy(load);
        bulkCopy.WriteToServer(source);
    }

    /// <summary>OracleBulkCopy requires an open connection, so this runs only inside a write. Disposed again if configuring it fails.</summary>
    private OracleBulkCopy CreateBulkCopy(DirectPathLoad load)
    {
        var bulkCopy = new OracleBulkCopy(connection, CopyOptions);
        try
        {
            Configure(bulkCopy, load);
            return bulkCopy;
        }
        catch
        {
            bulkCopy.Dispose();
            throw;
        }
    }

    private static void Configure(OracleBulkCopy bulkCopy, DirectPathLoad load)
    {
        bulkCopy.DestinationTableName = load.TableSql;
        if (load.SchemaSql is not null)
        {
            bulkCopy.DestinationSchemaName = load.SchemaSql;
        }

        bulkCopy.BatchSize = load.BatchSize;
        bulkCopy.BulkCopyTimeout = load.TimeoutSeconds;
        foreach (DirectPathColumn column in load.Columns)
        {
            bulkCopy.ColumnMappings.Add(new OracleBulkCopyColumnMapping(column.SourceOrdinal, column.DestinationSql));
        }
    }
}

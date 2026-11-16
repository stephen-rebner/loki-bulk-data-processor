using System;
using System.Collections.Generic;
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
/// <see cref="OracleWriteMode.ArrayBind"/>: one conventional-path INSERT executed once per chunk with every parameter bound
/// to an array of values. Runs on the context's connection inside its transaction (ODP.NET commands always join the
/// connection's active transaction); never commits, rolls back or disposes the connection or transaction.
/// </summary>
internal sealed class ArrayBindOracleWriter(OracleConnection connection, OracleTransaction transaction, BulkWriteOptions options,
    IOracleIdentifierResolver resolver, OracleTypeMap typeMap, ILogger logger) : IBulkWriter
{
    /// <summary>Rows per execution when BatchSize is 0 ("all rows" would need everything in memory). Benchmarked sweet spot.</summary>
    internal const int DefaultChunkSize = 5000;

    /// <summary>Upper bound on rows per execution, so a huge BatchSize cannot allocate large buffers; the benchmarks showed no gain above it.</summary>
    internal const int MaxChunkSize = 20_000;

    public async Task WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        OracleResolvedTarget resolved = await resolver.ResolveAsync(connection, target, options.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
        OracleBoundColumn[] columns = BindColumns(source, resolved);
        using OracleCommand command = CreateInsertCommand(resolved, columns);
        try
        {
            (int rows, int executions) = await CopyRowsAsync(source, command, columns, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Array-bind insert wrote {RowCount} rows in {ExecutionCount} executions", rows, executions);
        }
        finally
        {
            DisposeParameters(command);
        }
    }

    /// <summary>OracleParameter holds unmanaged resources and disposing the command does not dispose its parameters.</summary>
    private static void DisposeParameters(OracleCommand command)
    {
        foreach (OracleParameter parameter in command.Parameters)
        {
            parameter.Dispose();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>BatchSize 0 means <see cref="DefaultChunkSize"/>; larger values are capped at <see cref="MaxChunkSize"/>.</summary>
    internal static int ChunkSize(int batchSize) => batchSize <= 0 ? DefaultChunkSize : Math.Min(batchSize, MaxChunkSize);

    /// <summary>
    /// Upper bound on buffered values (rows x columns) per chunk: the buffer allocates one slot per value up front, so a wide table
    /// with a large BatchSize is limited to 1,000,000 slots (about 8 MB of references) rather than up to 100 million.
    /// </summary>
    internal const int MaxCellsPerChunk = 1_000_000;

    /// <summary><see cref="ChunkSize(int)"/>, further limited so rows x <paramref name="columnCount"/> stays within <see cref="MaxCellsPerChunk"/>.</summary>
    internal static int ChunkSize(int batchSize, int columnCount) =>
        Math.Min(ChunkSize(batchSize), Math.Max(1, MaxCellsPerChunk / Math.Max(1, columnCount)));

    /// <summary>Finds each mapped source column in the reader and picks its binding. Fails before any SQL runs.</summary>
    internal OracleBoundColumn[] BindColumns(IDataReader source, OracleResolvedTarget resolved)
    {
        if (resolved.Columns.Count == 0)
        {
            throw OracleValidation.InvalidOperation("There are no columns to copy: the source has no columns or every column was unmapped.");
        }

        return resolved.Columns.Select(column =>
        {
            int ordinal = SourceOrdinal(source, column.SourceColumn);
            return new OracleBoundColumn(ordinal, typeMap.For(source.GetFieldType(ordinal), column.SourceColumn));
        }).ToArray();
    }

    /// <summary>The INSERT with one positional input parameter per column; values are attached per chunk.</summary>
    internal OracleCommand CreateInsertCommand(OracleResolvedTarget resolved, IReadOnlyList<OracleBoundColumn> columns)
    {
        OracleCommand command = connection.CreateCommand();
        command.CommandText = OracleInsertSqlBuilder.Build(resolved);
        command.BindByName = false;
        command.CommandTimeout = options.TimeoutSeconds;   // per chunk execution
        command.Transaction = transaction;                 // informational: ODP.NET always uses the connection's transaction
        foreach (OracleBoundColumn column in columns)
        {
            command.Parameters.Add(new OracleParameter { OracleDbType = column.Binding.DbType, Direction = ParameterDirection.Input });
        }

        return command;
    }

    private Task<(int Rows, int Executions)> CopyRowsAsync(IDataReader source, OracleCommand command,
        IReadOnlyList<OracleBoundColumn> columns, CancellationToken cancellationToken) =>
        CopyInChunksAsync(source, new ColumnBuffer(columns, ChunkSize(options.BatchSize, columns.Count)),
            (chunk, token) => ExecuteChunkAsync(command, chunk, token), cancellationToken);

    /// <summary>Reads every row into the buffer and hands each full (and the final partial) chunk to <paramref name="executeChunk"/>.</summary>
    internal static async Task<(int Rows, int Executions)> CopyInChunksAsync(IDataReader source, ColumnBuffer buffer,
        Func<ColumnBuffer, CancellationToken, Task> executeChunk, CancellationToken cancellationToken)
    {
        int rows = 0, executions = 0;
        while (source.Read())
        {
            buffer.AddRow(source);
            if (buffer.IsFull)
            {
                rows += await FlushAsync(buffer, executeChunk, cancellationToken).ConfigureAwait(false);
                executions++;
            }
        }

        if (buffer.Count > 0)
        {
            rows += await FlushAsync(buffer, executeChunk, cancellationToken).ConfigureAwait(false);
            executions++;
        }

        return (rows, executions);
    }

    private static async Task<int> FlushAsync(ColumnBuffer buffer, Func<ColumnBuffer, CancellationToken, Task> executeChunk,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();   // also covers a slow source reader between flushes
        int count = buffer.Count;
        await executeChunk(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Clear();
        return count;
    }

    private static Task ExecuteChunkAsync(OracleCommand command, ColumnBuffer chunk, CancellationToken cancellationToken)
    {
        BindChunk(command, chunk);
        return command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Attaches one array per parameter; every array is exactly <c>ArrayBindCount</c> long.</summary>
    internal static void BindChunk(OracleCommand command, ColumnBuffer chunk)
    {
        command.ArrayBindCount = chunk.Count;
        for (int i = 0; i < command.Parameters.Count; i++)
        {
            command.Parameters[i].Value = chunk.ValuesFor(i);
        }
    }

    private static int SourceOrdinal(IDataReader source, string column)
    {
        int ordinal;
        try
        {
            ordinal = source.GetOrdinal(column);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException)
        {
            ordinal = -1;
        }

        return ordinal >= 0 ? ordinal : throw OracleValidation.InvalidOperation(BulkCopyErrorMessages.SourceColumnNotFound(column));
    }
}

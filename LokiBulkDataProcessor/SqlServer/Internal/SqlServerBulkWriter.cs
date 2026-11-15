using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Data.SqlClient;

namespace Loki.BulkDataProcessor.SqlServer.Internal;

/// <summary>One SqlBulkCopy per write, enlisted in the context's transaction. Never owns the connection or transaction.</summary>
internal sealed class SqlServerBulkWriter : IBulkWriter
{
    internal const SqlBulkCopyOptions CopyOptions = SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls;

    private readonly SqlBulkCopy _bulkCopy;

    public SqlServerBulkWriter(SqlConnection connection, SqlTransaction transaction, BulkWriteOptions options)
    {
        _bulkCopy = new SqlBulkCopy(connection, CopyOptions, transaction)
        {
            BatchSize = options.BatchSize,
            BulkCopyTimeout = options.TimeoutSeconds
        };
    }

    internal SqlBulkCopy BulkCopy => _bulkCopy;

    public async Task WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken cancellationToken)
    {
        ConfigureTarget(target);
        await _bulkCopy.WriteToServerAsync(source, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        ((IDisposable)_bulkCopy).Dispose();
        return ValueTask.CompletedTask;
    }

    internal void ConfigureTarget(BulkWriteTarget target)
    {
        _bulkCopy.DestinationTableName = target.TableName;   // SqlBulkCopy parses and brackets multipart names itself
        foreach (ColumnMap column in target.Columns)
        {
            _bulkCopy.ColumnMappings.Add(column.SourceColumn, column.DestinationColumn);
        }
    }
}

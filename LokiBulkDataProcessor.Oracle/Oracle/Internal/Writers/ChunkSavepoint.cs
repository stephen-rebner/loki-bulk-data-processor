using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>A savepoint taken before each array-bound chunk, so a failed chunk can be undone on its own.</summary>
internal interface IChunkSavepoint
{
    Task SetAsync(CancellationToken cancellationToken);

    Task RollBackAsync();
}

/// <summary>
/// <see cref="IChunkSavepoint"/> on the write's <see cref="OracleTransaction"/> (Loki's own or the caller's). ODP.NET runs array DML
/// in batch-error mode: when a row fails, the chunk's valid rows are still inserted. Rolling back to <see cref="Name"/> removes them, so
/// every chunk is all-or-nothing, as a SqlBulkCopy batch is. Rolling back to a savepoint undoes only the work done after it (Loki's own
/// rows) and never ends, commits or rolls back the transaction itself, so a caller's transaction stays theirs (#12).
/// </summary>
internal sealed class OracleChunkSavepoint(OracleTransaction transaction) : IChunkSavepoint
{
    /// <summary>A constant name, never built from caller input. Setting it again moves it to the start of the next chunk.</summary>
    internal const string Name = "LOKI_CHUNK";

    public Task SetAsync(CancellationToken cancellationToken) => transaction.SaveAsync(Name, cancellationToken);

    /// <summary>Not cancellable: it runs after the chunk failed, also when the failure was a cancellation.</summary>
    public Task RollBackAsync() => transaction.RollbackAsync(Name, CancellationToken.None);
}

/// <summary>No transaction to set a savepoint on (the pipeline always provides one; this keeps a bare writer usable).</summary>
internal sealed class NoChunkSavepoint : IChunkSavepoint
{
    public static readonly NoChunkSavepoint Instance = new();

    public Task SetAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollBackAsync() => Task.CompletedTask;
}

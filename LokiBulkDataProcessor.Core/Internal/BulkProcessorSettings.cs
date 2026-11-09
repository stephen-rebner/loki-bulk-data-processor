using System.Data;
using System.Diagnostics;
using Loki.BulkDataProcessor.DefaultValues;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>Settings owned by one processor instance, never shared between processors (#11).</summary>
internal sealed class BulkProcessorSettings
{
    public int BatchSize { get; private set; } = DefaultConfigValues.BatchSize;

    public int TimeoutSeconds { get; private set; } = DefaultConfigValues.Timeout;

    public IDbTransaction ExternalTransaction { get; private set; }

    public void SetBatchSize(int batchSize) => BatchSize = batchSize;

    public void SetTimeout(int timeoutSeconds) => TimeoutSeconds = timeoutSeconds;

    public void SetTransaction(IDbTransaction transaction) => ExternalTransaction = transaction;

    /// <summary>Copies the current settings, so a change made while a call runs does not affect that call.</summary>
    public BulkCallSettings Snapshot(string connectionString) =>
        new(connectionString, BatchSize, TimeoutSeconds, ExternalTransaction);
}

/// <summary>Immutable per-call copy. Deliberately not a record: a generated ToString would print the connection string.</summary>
internal readonly struct BulkCallSettings(string connectionString, int batchSize, int timeoutSeconds, IDbTransaction externalTransaction)
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string ConnectionString { get; } = connectionString;

    public int BatchSize { get; } = batchSize;

    public int TimeoutSeconds { get; } = timeoutSeconds;

    public IDbTransaction ExternalTransaction { get; } = externalTransaction;

    public override string ToString() => $"BatchSize={BatchSize}, Timeout={TimeoutSeconds}";
}

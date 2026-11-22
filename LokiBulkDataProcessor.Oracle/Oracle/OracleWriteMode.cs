namespace Loki.BulkDataProcessor.Oracle;

/// <summary>How the Oracle provider writes rows.</summary>
public enum OracleWriteMode
{
    /// <summary>
    /// Default. Conventional-path, array-bound <c>INSERT</c>: enforces primary key, unique, foreign key and check
    /// constraints, fires triggers, and is fully transactional (it joins Loki's or the caller's transaction).
    /// </summary>
    ArrayBind = 0,

    /// <summary>
    /// Opt-in <c>OracleBulkCopy</c> direct-path load. Faster on indexed tables, but with these hazards:
    /// <list type="bullet">
    /// <item>FOREIGN KEYS ARE NOT ENFORCED: orphan rows are loaded and the constraint still reports itself as validated.</item>
    /// <item>Duplicate primary or unique keys are loaded and leave the index UNUSABLE; later inserts and some queries on the table fail
    /// with ORA-01502 until the index is rebuilt.</item>
    /// <item>Tables with triggers are refused (ORA-26086).</item>
    /// <item>Only ONE load per transaction (ORA-39822): a second save in the same <see cref="IBulkProcessor.Transaction"/> fails, so
    /// commit between loads.</item>
    /// <item>Synchronous: ODP.NET has no asynchronous direct-path API, so the load blocks the calling thread and the cancellation token
    /// is only checked before it starts. <see cref="IBulkProcessor.Timeout"/> is passed to the driver but is not reliably honoured.</item>
    /// <item><see cref="OracleProviderOptions.BooleanBinding"/> does not apply: the driver converts values itself.</item>
    /// </list>
    /// A failed load leaves none of its rows behind. Use it only for tables where these do not matter, such as staging tables.
    /// </summary>
    DirectPath = 1
}

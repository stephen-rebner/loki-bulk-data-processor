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
    /// FOR BULK LOADS INTO STAGING TABLES ONLY. Do not use it for tables that other applications read or write, that have foreign keys or
    /// unique keys you rely on, or whose destination can be chosen by untrusted input. Opt-in <c>OracleBulkCopy</c> direct-path load: faster
    /// on indexed tables, with these hazards:
    /// <list type="bullet">
    /// <item>OTHER SESSIONS ARE BLOCKED: until the transaction that ran the load ends, other sessions' queries on the loaded table wait
    /// (library cache lock). Commit (or roll back) right after the save; with Loki's own transaction this lasts only for the save.</item>
    /// <item>FOREIGN KEYS ARE NOT ENFORCED: orphan rows are loaded and the constraint still reports itself as validated.</item>
    /// <item>Duplicate primary or unique keys make Oracle mark the index UNUSABLE when the transaction commits. Loki checks the table's
    /// unique indexes after the load and fails the save with an <see cref="System.InvalidOperationException"/> instead, so Loki's own
    /// transaction is rolled back (which leaves the index VALID). In your <see cref="IBulkProcessor.Transaction"/> you must roll back
    /// yourself: committing leaves the index UNUSABLE, so later inserts and deletes fail with ORA-01502 and later direct-path loads with
    /// ORA-26028 (queries and updates of other columns still work) until it is repaired; see the README section on repairing an UNUSABLE
    /// index. An index that was already UNUSABLE before the save also fails the check.</item>
    /// <item>Tables with triggers are refused (ORA-26086).</item>
    /// <item>Only ONE load per transaction (ORA-39822): a second save in the same <see cref="IBulkProcessor.Transaction"/> fails, also
    /// into another table and also after a failed load, so commit (or roll back) between loads. The transaction stays usable for other
    /// statements.</item>
    /// <item>Synchronous and not cancellable once started: ODP.NET has no asynchronous direct-path API, so the load blocks the calling
    /// thread and the cancellation token is only checked before it starts. <see cref="IBulkProcessor.Timeout"/> is passed to the driver
    /// but is NOT a reliable upper bound: a load waiting on another session's lock ran until the lock was released and only then failed
    /// with ORA-50057 (translated with the timeout hint), and nothing was saved.</item>
    /// <item><see cref="IBulkProcessor.BatchSize"/> is the number of rows per batch sent to the server; 0 means 5000, as with array bind.
    /// A failed load leaves none of its rows behind, whatever the BatchSize.</item>
    /// <item><see cref="OracleProviderOptions.BooleanBinding"/> does not apply: the driver converts values itself (1/0 into
    /// <c>NUMBER(1)</c>, TRUE/FALSE into <c>BOOLEAN</c>). Source column types are validated as for array bind, and <c>DateOnly</c> and
    /// <c>TimeOnly</c> are rejected before the load (ODP.NET's direct path cannot convert them; use <c>DateTime</c> / <c>TimeSpan</c>).</item>
    /// </list>
    /// Register it under its own name, separate from the default array-bind provider, for example
    /// <c>AddOracle(o =&gt; o.WriteMode = OracleWriteMode.DirectPath, name: "Oracle.DirectPath")</c>, and create processors from it only in
    /// code that targets staging tables the host has decided may skip foreign key and trigger enforcement. Never let a JSON payload's
    /// <c>tableName</c> or other untrusted input choose the destination of a direct-path processor: use
    /// <c>SaveAsync(stream, destinationTableName)</c> with a table name chosen by your code.
    /// </summary>
    DirectPath = 1
}

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
    /// Opt-in <c>OracleBulkCopy</c> direct-path load. Faster, but: synchronous (blocks the thread and cannot be cancelled once
    /// started); FOREIGN KEYS ARE NOT ENFORCED; duplicate primary keys are inserted and leave the primary key index UNUSABLE
    /// (later inserts fail with ORA-01502); tables with triggers are refused (ORA-26086); only ONE load per transaction
    /// (ORA-39822); the timeout is not reliably honoured.
    /// </summary>
    /// <remarks>Not available in this build yet: <c>AddOracle</c> throws <see cref="System.NotSupportedException"/> when it is selected.</remarks>
    DirectPath = 1
}

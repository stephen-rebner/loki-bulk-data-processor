namespace Loki.BulkDataProcessor.Oracle;

/// <summary>The Oracle provider (Oracle.ManagedDataAccess.Core, Oracle Database 19c and later).</summary>
/// <remarks>
/// <para>
/// Rows are written with a conventional-path, array-bound <c>INSERT</c> in chunks of <see cref="IBulkProcessor.BatchSize"/>
/// rows (0 means 5000), inside Loki's own transaction or the caller's <see cref="IBulkProcessor.Transaction"/>. Primary,
/// unique, foreign key and check constraints are enforced and INSERT triggers fire. <see cref="IBulkProcessor.Timeout"/>
/// applies to each chunk.
/// </para>
/// <para>
/// Oracle stores an empty string as NULL. Destination names are matched as described on <see cref="OracleIdentifierResolution"/>.
/// </para>
/// </remarks>
public static class OracleProvider
{
    /// <summary>Default registered name: pass it to <see cref="IBulkProcessorFactory.Create"/>.</summary>
    public const string Name = "Oracle";
}

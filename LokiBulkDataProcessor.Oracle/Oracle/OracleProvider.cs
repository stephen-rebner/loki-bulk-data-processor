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
/// Unlike SqlBulkCopy, which streams, each chunk is buffered in memory before it is sent. A chunk is also cut short at 1,000,000
/// values (rows x columns) or about 64 MB of string and binary data, whichever comes first, so a large BatchSize or wide rows
/// cannot exhaust memory; the rows are still written in the same transaction.
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

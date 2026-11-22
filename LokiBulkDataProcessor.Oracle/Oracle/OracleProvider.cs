namespace Loki.BulkDataProcessor.Oracle;

/// <summary>The Oracle provider (Oracle.ManagedDataAccess.Core, Oracle Database 19c and later).</summary>
/// <remarks>
/// <para>
/// Rows are written with a conventional-path, array-bound <c>INSERT</c> in chunks of <see cref="IBulkProcessor.BatchSize"/>
/// rows (0 means 5000), inside Loki's own transaction or the caller's <see cref="IBulkProcessor.Transaction"/>. Primary,
/// unique, foreign key and check constraints are enforced and INSERT triggers fire. <see cref="IBulkProcessor.Timeout"/>
/// applies to each chunk; an expired Timeout throws an <see cref="System.InvalidOperationException"/> (ORA-01013) with the driver
/// error as InnerException, and a cancelled token throws an <see cref="System.OperationCanceledException"/>.
/// </para>
/// <para>
/// Each chunk is all-or-nothing, like a SqlBulkCopy batch: Loki sets a savepoint before every chunk and, if any row of the chunk fails,
/// rolls back to it (ODP.NET would otherwise keep the chunk's valid rows). Rolling back to that savepoint never ends your transaction.
/// Chunks that succeeded before the failure stay in your <see cref="IBulkProcessor.Transaction"/>, so roll back your transaction after a
/// failed save unless you mean to keep them. With Loki's own transaction the whole save is rolled back.
/// </para>
/// <para>
/// ODP.NET delivers a timeout or cancellation to the server as a break. When out-of-band breaks are disabled (<c>DISABLE_OOB=ON</c>
/// in the server's or client's <c>sqlnet.ora</c>, common in containers, for example the gvenzl/oracle-free images), the server acts on it
/// only when the current call returns: a chunk that waits on a lock held by another session is not interrupted until that lock is released.
/// </para>
/// <para>
/// Unlike SqlBulkCopy, which streams, each chunk is buffered in memory before it is sent. A chunk is also cut short at 1,000,000
/// values (rows x columns) or about 64 MB of string and binary data, whichever comes first, so a large BatchSize or wide rows
/// cannot exhaust memory; the rows are still written in the same transaction.
/// </para>
/// <para>
/// The paragraphs above describe the default <see cref="OracleWriteMode.ArrayBind"/>. <see cref="OracleWriteMode.DirectPath"/> is for bulk
/// loads into staging tables only: never use it for tables other applications use, tables whose foreign or unique keys you rely on, or a
/// destination chosen by untrusted input, and register it under its own name. Each save is one synchronous <c>OracleBulkCopy</c> load (it
/// blocks the calling thread, the cancellation token is checked only before the load starts, and <see cref="IBulkProcessor.Timeout"/> is not
/// a reliable upper bound) that streams the source in batches of <see cref="IBulkProcessor.BatchSize"/> rows (0 means 5000). It joins your
/// <see cref="IBulkProcessor.Transaction"/>, and until that transaction ends other sessions' queries on the table wait, so commit right
/// after the save. Only one load is allowed per transaction (ORA-39822); the transaction stays usable for other statements (not for
/// another direct-path load). A failed load leaves none of its rows. Foreign keys are not enforced, tables with triggers are refused
/// (ORA-26086), and a load with duplicate keys fails the save so that it can be rolled back (committing it would leave the index UNUSABLE):
/// see <see cref="OracleWriteMode.DirectPath"/>.
/// </para>
/// <para>
/// Oracle stores an empty string as NULL. Destination names are matched as described on <see cref="OracleIdentifierResolution"/>.
/// An invalid destination table name throws an <see cref="System.ArgumentException"/> (<c>ParamName</c> "destinationTableName") before
/// any SQL runs, where SQL Server reports it as a failed copy (<see cref="System.InvalidOperationException"/>); an invalid destination
/// column throws an <see cref="System.InvalidOperationException"/> starting with <see cref="Providers.BulkCopyErrorMessages.ColumnMappingMismatch"/>
/// on both.
/// </para>
/// <para>
/// Strings are bound as <c>VARCHAR2</c> in the database character set, so a value for a <c>VARCHAR2</c> column must fit the column
/// (ORA-12899 otherwise). <c>CLOB</c> columns accepted longer values (tested up to 100,000 characters on Oracle Database 23.26 with
/// <c>MAX_STRING_SIZE = STANDARD</c>); an older server may reject a value over the VARCHAR2 bind limit (4000 bytes, or 32767 with
/// <c>MAX_STRING_SIZE = EXTENDED</c>) with ORA-01461. <c>NVARCHAR2</c> columns work, but characters outside the database character
/// set may be replaced when it is not AL32UTF8. <c>byte[]</c> binds as <c>RAW</c> (BLOB is not supported); <c>DateOnly</c> binds as <c>DATE</c> and <c>TimeOnly</c> as
/// <c>INTERVAL DAY TO SECOND</c>.
/// </para>
/// </remarks>
public static class OracleProvider
{
    /// <summary>Default registered name: pass it to <see cref="IBulkProcessorFactory.Create"/>.</summary>
    public const string Name = "Oracle";
}

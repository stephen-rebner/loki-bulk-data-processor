using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>One mapped column of a direct-path load: the source reader's ordinal and the quoted destination column.</summary>
internal sealed record DirectPathColumn(int SourceOrdinal, string DestinationSql);

/// <summary>
/// Everything one <c>OracleBulkCopy</c> load is configured with, built before the load starts (and without a database, so it is
/// unit-tested). Every name is the resolver's validated, quoted identifier.
/// </summary>
/// <param name="SchemaSql">Quoted schema, or null for the connection's current schema.</param>
/// <param name="TableSql">Quoted table name.</param>
/// <param name="BatchSize">Rows per batch sent to the server; 0 sends all rows as one batch.</param>
/// <param name="TimeoutSeconds">Passed to <c>OracleBulkCopy.BulkCopyTimeout</c>; 0 means no limit.</param>
/// <param name="Columns">The mapped columns, in target order.</param>
internal sealed record DirectPathLoad(string SchemaSql, string TableSql, int BatchSize, int TimeoutSeconds,
    IReadOnlyList<DirectPathColumn> Columns);

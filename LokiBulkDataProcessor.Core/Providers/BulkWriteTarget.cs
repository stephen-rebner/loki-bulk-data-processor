using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Providers;

/// <summary>The destination of a bulk write: a table name and the column maps to apply.</summary>
/// <param name="TableName">Destination table name as supplied by the caller (it may be schema-qualified).</param>
/// <param name="Columns">Source-to-destination column maps, in source order.</param>
public sealed record BulkWriteTarget(string TableName, IReadOnlyList<ColumnMap> Columns);

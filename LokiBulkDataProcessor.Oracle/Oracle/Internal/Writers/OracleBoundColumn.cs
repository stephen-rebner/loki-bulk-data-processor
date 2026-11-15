using Loki.BulkDataProcessor.Oracle.Internal.Types;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>A destination column's source ordinal and binding, in INSERT column order.</summary>
internal sealed record OracleBoundColumn(int SourceOrdinal, OracleBinding Binding);

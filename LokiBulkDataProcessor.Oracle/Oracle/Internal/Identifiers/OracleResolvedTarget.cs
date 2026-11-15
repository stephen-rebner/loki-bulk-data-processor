using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>One destination column: the caller's source column and the validated, quoted destination SQL identifier.</summary>
internal sealed record OracleResolvedColumn(string SourceColumn, string DestinationSql);

/// <summary>A destination whose names are safe to put into SQL: every part is validated and quoted by a resolver.</summary>
/// <param name="SchemaSql">Quoted schema, or null for the connection's current schema.</param>
/// <param name="TableSql">Quoted table name.</param>
/// <param name="Columns">Columns in source-map order.</param>
internal sealed record OracleResolvedTarget(string SchemaSql, string TableSql, IReadOnlyList<OracleResolvedColumn> Columns)
{
    public string QualifiedTableSql => SchemaSql is null ? TableSql : $"{SchemaSql}.{TableSql}";
}

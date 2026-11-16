using System.Linq;
using System.Text;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>Builds the array-bound INSERT, for example <c>INSERT INTO "POSTS" ("TITLE", "BLOGID") VALUES (:1, :2)</c>.</summary>
internal static class OracleInsertSqlBuilder
{
    /// <remarks>Only identifiers already validated and quoted by an <see cref="IOracleIdentifierResolver"/> are concatenated;
    /// values are always bound by position (CWE-89).</remarks>
    public static string Build(OracleResolvedTarget target)
    {
        StringBuilder sql = new StringBuilder("INSERT INTO ").Append(target.QualifiedTableSql).Append(" (");
        sql.AppendJoin(", ", target.Columns.Select(column => column.DestinationSql)).Append(") VALUES (");
        sql.AppendJoin(", ", Enumerable.Range(1, target.Columns.Count).Select(position => $":{position}")).Append(')');
        return sql.ToString();
    }
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Providers;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>
/// <see cref="OracleIdentifierResolution.Unquoted"/>: every name must be a simple identifier; it is folded to upper case and
/// quoted (<c>Posts</c> becomes <c>"POSTS"</c>), which is what Oracle does with an unquoted name and also allows reserved words.
/// No database round trip.
/// </summary>
internal sealed class UnquotedIdentifierResolver : IOracleIdentifierResolver
{
    public Task<OracleResolvedTarget> ResolveAsync(OracleConnection connection, BulkWriteTarget target, int timeoutSeconds,
        CancellationToken cancellationToken) =>
        Task.FromResult(Resolve(target));

    internal static OracleResolvedTarget Resolve(BulkWriteTarget target)
    {
        (string schema, string table) = OracleIdentifier.Split(target.TableName);
        string schemaSql = schema is null ? null : OracleIdentifier.QuoteFolded(OracleIdentifier.RequireSimpleTableName(schema, "schema"));
        string tableSql = OracleIdentifier.QuoteFolded(OracleIdentifier.RequireSimpleTableName(table, "table"));
        OracleResolvedColumn[] columns = target.Columns
            .Select(column => new OracleResolvedColumn(column.SourceColumn,
                OracleIdentifier.QuoteFolded(OracleIdentifier.RequireSimpleColumnName(column.DestinationColumn))))
            .ToArray();
        return new OracleResolvedTarget(schemaSql, tableSql, columns);
    }
}

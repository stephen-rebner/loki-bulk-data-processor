using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// After a direct-path load, fails the save when the load left a unique index of the destination table UNUSABLE (the rows contained
/// duplicate keys), so the pipeline rolls back Loki's own transaction instead of committing an UNUSABLE index.
/// </summary>
/// <remarks>
/// Verified on Oracle 23.26 (step 8 review): inside the loading transaction <c>ALL_INDEXES.STATUS</c> still says VALID (Oracle records
/// UNUSABLE only at commit, and a rollback leaves the index VALID), but any access to the index already fails with ORA-01502. So the
/// check lists the table's unique B-tree indexes with a constant catalogue query (names as bind variables) and then touches each one
/// with a one-row index fast full scan. The probe's identifiers come from the catalogue and are quoted by
/// <see cref="OracleIdentifier.Quote"/> (rejects a double quote or NUL), the same rule as <see cref="CatalogIdentifierResolver"/>.
/// Function-based, bitmap, domain and index-organized indexes are not probed (a load cannot leave the first three UNUSABLE through
/// duplicate keys, and direct path does not load index-organized tables).
/// </remarks>
internal static class UnusableIndexCheck
{
    /// <summary>The table's unique B-tree indexes and their leading column. With no owner, the session's current schema.</summary>
    internal const string UniqueIndexQuery = """
        SELECT i.INDEX_NAME, c.COLUMN_NAME, i.PARTITIONED FROM ALL_INDEXES i
        JOIN ALL_IND_COLUMNS c ON c.INDEX_OWNER = i.OWNER AND c.INDEX_NAME = i.INDEX_NAME AND c.COLUMN_POSITION = 1
        WHERE i.TABLE_OWNER = NVL(:owner, SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')) AND i.TABLE_NAME = :tableName
        AND i.UNIQUENESS = 'UNIQUE' AND i.INDEX_TYPE IN ('NORMAL', 'NORMAL/REV')
        """;

    /// <summary>ORA-01502: index or index partition is in unusable state.</summary>
    internal const int IndexUnusable = 1502;

    /// <summary>The fixed, data-free message; only the caller's (sanitised) table name is echoed.</summary>
    internal static string Message(string tableName) =>
        $"The direct-path load into '{BulkCopyErrorMessages.Sanitize(tableName)}' left a unique index of the table UNUSABLE (typically " +
        "because the rows contained duplicate primary or unique keys), so the save was failed so that it can be rolled back. Loki rolls " +
        "back its own transaction; if the save ran in your IBulkProcessor.Transaction, roll that transaction back (committing it would " +
        "leave the index UNUSABLE). Rolling back restores the index.";

    /// <summary>Throws a marked <see cref="System.InvalidOperationException"/> when a unique index of the target is UNUSABLE.</summary>
    public static async Task ThrowIfAnyUnusableAsync(OracleConnection connection, OracleResolvedTarget resolved, string requestedTableName,
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        IReadOnlyList<UniqueIndex> indexes = await ReadUniqueIndexesAsync(connection, resolved, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        foreach (UniqueIndex index in indexes)
        {
            if (!await IsUsableAsync(connection, ProbeSql(resolved, index), timeoutSeconds, cancellationToken).ConfigureAwait(false))
            {
                throw OracleValidation.InvalidOperation(Message(requestedTableName));
            }
        }
    }

    /// <summary>
    /// A one-row index fast full scan of <paramref name="index"/> (rows whose leading column is NULL may be missing from the index, so
    /// they are filtered out to make the hint applicable). A partitioned index is scanned completely, so every partition is touched.
    /// </summary>
    internal static string ProbeSql(OracleResolvedTarget resolved, UniqueIndex index)
    {
        string probe = $"SELECT /*+ INDEX_FFS(t {OracleIdentifier.Quote(index.Name)}) */ COUNT(*) FROM {resolved.QualifiedTableSql} t " +
            $"WHERE {OracleIdentifier.Quote(index.LeadingColumn)} IS NOT NULL";
        return index.Partitioned ? probe : $"{probe} AND ROWNUM = 1";
    }

    private static async Task<IReadOnlyList<UniqueIndex>> ReadUniqueIndexesAsync(OracleConnection connection, OracleResolvedTarget resolved,
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        using OracleCommand command = CatalogIdentifierResolver.CreateCatalogCommand(connection, UniqueIndexQuery,
            OwnerOf(resolved), OracleIdentifier.StripQuotes(resolved.TableSql), timeoutSeconds);
        using DbDataReader reader = await OracleCommandTimeout.RunAsync(command.ExecuteReaderAsync, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        List<UniqueIndex> indexes = [];
        while (await OracleCommandTimeout.RunAsync(reader.ReadAsync, timeoutSeconds, cancellationToken).ConfigureAwait(false))
        {
            indexes.Add(new UniqueIndex(reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
        }

        return indexes;
    }

    /// <summary>False when the probe fails with ORA-01502; any other error propagates (and fails the save).</summary>
    private static async Task<bool> IsUsableAsync(OracleConnection connection, string probeSql, int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using OracleCommand command = connection.CreateCommand();
        command.CommandText = probeSql;
        command.CommandTimeout = timeoutSeconds;
        try
        {
            await OracleCommandTimeout.RunAsync(command.ExecuteScalarAsync, timeoutSeconds, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OracleException exception) when (exception.Number == IndexUnusable)
        {
            return false;
        }
    }

    /// <summary>The resolved schema without its quotes, or null for the session's current schema.</summary>
    internal static string OwnerOf(OracleResolvedTarget resolved) =>
        resolved.SchemaSql is null ? null : OracleIdentifier.StripQuotes(resolved.SchemaSql);

    /// <summary>One unique index of the destination table, as named in the catalogue.</summary>
    internal sealed record UniqueIndex(string Name, string LeadingColumn, bool Partitioned);
}

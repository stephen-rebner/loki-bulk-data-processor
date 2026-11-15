using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Providers;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>
/// <see cref="OracleIdentifierResolution.CatalogLookup"/>: finds the table and columns in <c>ALL_TAB_COLUMNS</c> (exact match
/// first, then a single case-insensitive match; a name in double quotes matches exactly only) and quotes the catalogue's names.
/// The caller's names are only ever bind variables; only catalogue names reach SQL, and <see cref="OracleIdentifier.Quote"/>
/// rejects any containing a double quote or NUL (CWE-89).
/// </summary>
internal sealed class CatalogIdentifierResolver : IOracleIdentifierResolver
{
    internal const string CatalogQuery =
        "SELECT OWNER, TABLE_NAME, COLUMN_NAME FROM ALL_TAB_COLUMNS " +
        "WHERE UPPER(OWNER) = UPPER(NVL(:owner, SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'))) " +
        "AND UPPER(TABLE_NAME) = UPPER(:tableName)";

    public async Task<OracleResolvedTarget> ResolveAsync(OracleConnection connection, BulkWriteTarget target, CancellationToken cancellationToken)
    {
        (string schema, string table) = OracleIdentifier.Split(target.TableName);
        IReadOnlyList<CatalogRow> rows = await QueryAsync(connection,
            OracleIdentifier.StripQuotes(schema), OracleIdentifier.StripQuotes(table), cancellationToken).ConfigureAwait(false);
        return Resolve(rows, target);
    }

    /// <summary>Picks the table, owner and columns from the catalogue rows. Pure, so it is unit-tested without a database.</summary>
    internal static OracleResolvedTarget Resolve(IReadOnlyList<CatalogRow> rows, BulkWriteTarget target)
    {
        (string schema, string table) = OracleIdentifier.Split(target.TableName);
        (string owner, string tableName) = PickTable(rows, schema, table, target.TableName);
        string[] tableColumns = rows.Where(r => r.Owner == owner && r.Table == tableName).Select(r => r.Column).ToArray();
        OracleResolvedColumn[] columns = target.Columns
            .Select(c => new OracleResolvedColumn(c.SourceColumn, OracleIdentifier.Quote(PickColumn(tableColumns, c.DestinationColumn))))
            .ToArray();
        return new OracleResolvedTarget(OracleIdentifier.Quote(owner), OracleIdentifier.Quote(tableName), columns);
    }

    private static async Task<IReadOnlyList<CatalogRow>> QueryAsync(OracleConnection connection, string owner, string table,
        CancellationToken cancellationToken)
    {
        using OracleCommand command = CreateCatalogCommand(connection, owner, table);
        List<CatalogRow> rows = [];
        using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    internal static OracleCommand CreateCatalogCommand(OracleConnection connection, string owner, string table)
    {
        OracleCommand command = connection.CreateCommand();
        command.CommandText = CatalogQuery;
        command.BindByName = true;
        command.Parameters.Add("owner", OracleDbType.Varchar2).Value = (object)owner ?? DBNull.Value;
        command.Parameters.Add("tableName", OracleDbType.Varchar2).Value = table ?? string.Empty;
        return command;
    }

    private static (string Owner, string Table) PickTable(IReadOnlyList<CatalogRow> rows, string schema, string table, string requestedTable)
    {
        string notFound = $"The destination table '{SafeText.Sanitize(requestedTable)}' was not found or is not visible to the connected user.";
        string tableName = PickName(rows.Select(r => r.Table).Distinct().ToArray(), table, "table", notFound);
        string[] owners = rows.Where(r => r.Table == tableName).Select(r => r.Owner).Distinct().ToArray();
        string owner = PickName(owners, schema ?? owners.FirstOrDefault(), "schema", notFound);
        return (owner, tableName);
    }

    private static string PickColumn(IReadOnlyCollection<string> tableColumns, string requested) =>
        PickName(tableColumns, requested, "column",
            $"{BulkCopyErrorMessages.ColumnMappingMismatch} ('{SafeText.Sanitize(OracleIdentifier.StripQuotes(requested))}' not found)");

    /// <summary>Exact match first, then a single case-insensitive match; otherwise a clear error. A quoted request matches exactly only.</summary>
    internal static string PickName(IReadOnlyCollection<string> candidates, string requested, string role, string notFoundMessage)
    {
        if (string.IsNullOrEmpty(requested) || candidates.Count == 0)
        {
            throw OracleValidation.InvalidOperation(notFoundMessage);
        }

        string name = OracleIdentifier.StripQuotes(requested);
        string exact = candidates.FirstOrDefault(c => c == name);
        if (exact is not null || OracleIdentifier.IsQuoted(requested))
        {
            return exact ?? throw OracleValidation.InvalidOperation(notFoundMessage);
        }

        return PickCaseInsensitive(candidates, name, role, notFoundMessage);
    }

    private static string PickCaseInsensitive(IReadOnlyCollection<string> candidates, string name, string role, string notFoundMessage)
    {
        string[] folded = candidates.Where(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return folded.Length switch
        {
            1 => folded[0],
            0 => throw OracleValidation.InvalidOperation(notFoundMessage),
            _ => throw OracleValidation.InvalidOperation(
                $"The {role} '{SafeText.Sanitize(name)}' matches more than one object case-insensitively; quote it to choose exactly.")
        };
    }

    internal sealed record CatalogRow(string Owner, string Table, string Column);
}

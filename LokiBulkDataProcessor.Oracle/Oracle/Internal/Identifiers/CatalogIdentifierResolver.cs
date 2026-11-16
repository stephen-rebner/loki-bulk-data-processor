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
    /// <summary>
    /// Fast path. Compares the dictionary columns exactly, with the caller's name as given and as Oracle folds an unquoted name
    /// (<c>UPPER</c> on the bind side only), so Oracle can use the dictionary indexes. With no schema, only the current schema is searched.
    /// </summary>
    internal const string ExactCatalogQuery = """
        SELECT OWNER, TABLE_NAME, COLUMN_NAME FROM ALL_TAB_COLUMNS
        WHERE OWNER IN (NVL(:owner, SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')), UPPER(:owner))
        AND TABLE_NAME IN (:tableName, UPPER(:tableName))
        """;

    /// <summary>
    /// Fallback for an unquoted name whose object is stored in mixed case (<c>mixedcase</c> for <c>"MixedCase"</c>). It is
    /// case-insensitive, so it scans the dictionary; it runs only when <see cref="ExactCatalogQuery"/> finds nothing.
    /// </summary>
    internal const string CaseInsensitiveCatalogQuery = """
        SELECT OWNER, TABLE_NAME, COLUMN_NAME FROM ALL_TAB_COLUMNS
        WHERE ((:owner IS NULL AND OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')) OR UPPER(OWNER) = UPPER(:owner))
        AND UPPER(TABLE_NAME) = UPPER(:tableName)
        """;

    public async Task<OracleResolvedTarget> ResolveAsync(OracleConnection connection, BulkWriteTarget target, int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        (string schema, string table) = OracleIdentifier.Split(target.TableName);
        string owner = OracleIdentifier.StripQuotes(schema);
        string tableName = OracleIdentifier.StripQuotes(table);
        IReadOnlyList<CatalogRow> rows = await QueryAsync(connection, ExactCatalogQuery, owner, tableName, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0 && NeedsCaseInsensitiveFallback(schema, table))
        {
            rows = await QueryAsync(connection, CaseInsensitiveCatalogQuery, owner, tableName, timeoutSeconds, cancellationToken)
                .ConfigureAwait(false);
        }

        return Resolve(rows, target);
    }

    /// <summary>A quoted name must match exactly, so the fallback can only help when the table or a given schema is unquoted.</summary>
    internal static bool NeedsCaseInsensitiveFallback(string schema, string table) =>
        !OracleIdentifier.IsQuoted(table) || (schema is not null && !OracleIdentifier.IsQuoted(schema));

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

    private static async Task<IReadOnlyList<CatalogRow>> QueryAsync(OracleConnection connection, string query, string owner, string table,
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        using OracleCommand command = CreateCatalogCommand(connection, query, owner, table, timeoutSeconds);
        List<CatalogRow> rows = [];
        using DbDataReader reader = await OracleCommandTimeout.RunAsync(command.ExecuteReaderAsync, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        while (await OracleCommandTimeout.RunAsync(reader.ReadAsync, timeoutSeconds, cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>Builds a catalogue query with the caller's names as bind variables and <paramref name="timeoutSeconds"/> as its timeout.</summary>
    internal static OracleCommand CreateCatalogCommand(OracleConnection connection, string query, string owner, string table, int timeoutSeconds)
    {
        OracleCommand command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = timeoutSeconds;
        command.BindByName = true;
        command.Parameters.Add("owner", OracleDbType.Varchar2).Value = (object)owner ?? DBNull.Value;
        command.Parameters.Add("tableName", OracleDbType.Varchar2).Value = table ?? string.Empty;
        return command;
    }

    private static (string Owner, string Table) PickTable(IReadOnlyList<CatalogRow> rows, string schema, string table, string requestedTable)
    {
        Func<string> notFound = () =>
            $"The destination table '{BulkCopyErrorMessages.Sanitize(requestedTable)}' was not found or is not visible to the connected user.";
        string tableName = PickName(rows.Select(r => r.Table).Distinct().ToArray(), table, "table", notFound);
        string[] owners = rows.Where(r => r.Table == tableName).Select(r => r.Owner).Distinct().ToArray();
        // With no schema both queries return only the current schema, so there is at most one owner here.
        string owner = PickName(owners, schema ?? owners.FirstOrDefault(), "schema", notFound);
        return (owner, tableName);
    }

    private static string PickColumn(IReadOnlyCollection<string> tableColumns, string requested) =>
        PickName(tableColumns, requested, "column", () =>
            $"{BulkCopyErrorMessages.ColumnMappingMismatch} ('{BulkCopyErrorMessages.Sanitize(OracleIdentifier.StripQuotes(requested))}' not found)");

    /// <summary>Exact match first, then a single case-insensitive match; otherwise a clear error. A quoted request matches exactly only.</summary>
    /// <param name="notFoundMessage">Built only when the name is not found, so a successful lookup allocates no message.</param>
    internal static string PickName(IReadOnlyCollection<string> candidates, string requested, string role, Func<string> notFoundMessage)
    {
        if (string.IsNullOrEmpty(requested) || candidates.Count == 0)
        {
            throw OracleValidation.InvalidOperation(notFoundMessage());
        }

        string name = OracleIdentifier.StripQuotes(requested);
        string exact = candidates.FirstOrDefault(c => c == name);
        if (exact is not null || OracleIdentifier.IsQuoted(requested))
        {
            return exact ?? throw OracleValidation.InvalidOperation(notFoundMessage());
        }

        return PickCaseInsensitive(candidates, name, role, notFoundMessage);
    }

    private static string PickCaseInsensitive(IReadOnlyCollection<string> candidates, string name, string role, Func<string> notFoundMessage)
    {
        string[] folded = candidates.Where(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return folded.Length switch
        {
            1 => folded[0],
            0 => throw OracleValidation.InvalidOperation(notFoundMessage()),
            _ => throw OracleValidation.InvalidOperation(
                $"The {role} '{BulkCopyErrorMessages.Sanitize(name)}' matches more than one object case-insensitively; quote it to choose exactly.")
        };
    }

    internal sealed record CatalogRow(string Owner, string Table, string Column);
}

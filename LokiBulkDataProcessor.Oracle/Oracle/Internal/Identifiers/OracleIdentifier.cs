using System.Text.RegularExpressions;
using Loki.BulkDataProcessor.Providers;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>
/// Validation and quoting of Oracle identifiers (CWE-89). Destination names can come from a JSON payload, so they are never
/// trusted: only names that passed <see cref="RequireSimpleTableName"/> / <see cref="RequireSimpleColumnName"/> or came back from the catalogue are put into SQL, always quoted.
/// </summary>
internal static partial class OracleIdentifier
{
    internal const int MaxLength = 128;

    private const string SimpleRule = "letter first, then letters, digits, _ $ #, max 128";

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_$#]{0,127}\z", RegexOptions.CultureInvariant)]  // \z, not $: $ also matches before a trailing \n
    private static partial Regex SimpleIdentifier();

    /// <summary>
    /// The <c>ParamName</c> of the <see cref="System.ArgumentException"/> for a rejected table or schema name: the
    /// <c>IBulkProcessor.SaveAsync</c> parameter the name came from.
    /// </summary>
    internal const string DestinationTableParameter = "destinationTableName";

    /// <summary>True when <paramref name="name"/> is a simple (unquoted) identifier.</summary>
    public static bool IsSimple(string name) => name is not null && SimpleIdentifier().IsMatch(name);

    /// <summary>The data-free reason a name is not a simple identifier; <paramref name="role"/> is "table", "schema" or "destination column".</summary>
    public static string NotSimpleMessage(string role, string name) =>
        $"The {role} '{BulkCopyErrorMessages.Sanitize(name)}' is not a valid unquoted Oracle identifier ({SimpleRule}). Use OracleIdentifierResolution.CatalogLookup for quoted mixed-case names.";

    /// <summary>
    /// Returns a valid table or schema name; otherwise throws a marked <see cref="System.ArgumentException"/> whose <c>ParamName</c>
    /// is <see cref="DestinationTableParameter"/>.
    /// </summary>
    public static string RequireSimpleTableName(string name, string role) =>
        IsSimple(name) ? name : throw OracleValidation.Argument(NotSimpleMessage(role, name), DestinationTableParameter);

    /// <summary>
    /// Returns a valid destination column; otherwise throws a marked <see cref="System.InvalidOperationException"/> that starts with
    /// <see cref="BulkCopyErrorMessages.ColumnMappingMismatch"/>, the same type and prefix SQL Server gives for a destination column
    /// it cannot use. Column names come from mappings or the source, not from a <c>SaveAsync</c> argument.
    /// </summary>
    public static string RequireSimpleColumnName(string name) =>
        IsSimple(name)
            ? name
            : throw OracleValidation.InvalidOperation($"{BulkCopyErrorMessages.ColumnMappingMismatch} ({NotSimpleMessage("destination column", name)})");

    /// <summary>Folds a validated simple identifier to upper case (what Oracle does with unquoted names) and quotes it.</summary>
    public static string QuoteFolded(string simpleName) => Quote(simpleName.ToUpperInvariant());

    /// <summary>Quotes an exact (catalogue) name. Oracle names can never contain a double quote or NUL.</summary>
    public static string Quote(string exactName) =>
        !string.IsNullOrEmpty(exactName) && exactName.Length <= MaxLength && exactName.IndexOfAny(['"', '\0']) < 0
            ? $"\"{exactName}\""
            : throw OracleValidation.InvalidOperation("The Oracle catalogue returned an object name that cannot be quoted safely.");

    /// <summary>Splits <c>TABLE</c> or <c>SCHEMA.TABLE</c>. A quoted name may not contain a dot.</summary>
    public static (string Schema, string Table) Split(string qualifiedName)
    {
        string[] parts = (qualifiedName ?? string.Empty).Split('.');
        return parts.Length switch
        {
            1 => (null, parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw OracleValidation.Argument(
                $"The destination table '{BulkCopyErrorMessages.Sanitize(qualifiedName)}' must be 'TABLE' or 'SCHEMA.TABLE'.", DestinationTableParameter)
        };
    }

    /// <summary>True when the name is wrapped in double quotes, which asks for an exact (case-sensitive) match.</summary>
    public static bool IsQuoted(string name) => name is { Length: > 1 } && name[0] == '"' && name[^1] == '"';

    /// <summary>Removes one pair of surrounding double quotes.</summary>
    public static string StripQuotes(string name) => IsQuoted(name) ? name[1..^1] : name;
}

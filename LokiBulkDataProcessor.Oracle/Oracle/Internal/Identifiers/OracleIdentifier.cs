using System.Text.RegularExpressions;
using Loki.BulkDataProcessor.Providers;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>
/// Validation and quoting of Oracle identifiers (CWE-89). Destination names can come from a JSON payload, so they are never
/// trusted: only names that passed <see cref="RequireSimple"/> or came back from the catalogue are put into SQL, always quoted.
/// </summary>
internal static partial class OracleIdentifier
{
    internal const int MaxLength = 128;

    private const string SimpleRule = "letter first, then letters, digits, _ $ #, max 128";

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_$#]{0,127}\z", RegexOptions.CultureInvariant)]  // \z, not $: $ also matches before a trailing \n
    private static partial Regex SimpleIdentifier();

    /// <summary>Returns <paramref name="name"/> when it is a simple (unquoted) identifier; otherwise throws a marked <c>ArgumentException</c>.</summary>
    /// <param name="name">The caller's name.</param>
    /// <param name="role">What the name is, for the message ("table", "schema", "destination column").</param>
    /// <param name="paramName">Reported as <c>ArgumentException.ParamName</c>.</param>
    public static string RequireSimple(string name, string role, string paramName) =>
        name is not null && SimpleIdentifier().IsMatch(name)
            ? name
            : throw OracleValidation.Argument(
                $"The {role} '{SafeText.Sanitize(name)}' is not a valid unquoted Oracle identifier ({SimpleRule}). " +
                "Use OracleIdentifierResolution.CatalogLookup for quoted mixed-case names.", paramName);

    /// <summary>Folds a validated simple identifier to upper case (what Oracle does with unquoted names) and quotes it.</summary>
    public static string QuoteFolded(string simpleName) => Quote(simpleName.ToUpperInvariant());

    /// <summary>Quotes an exact (catalogue) name. Oracle names can never contain a double quote or NUL.</summary>
    public static string Quote(string exactName) =>
        !string.IsNullOrEmpty(exactName) && exactName.Length <= MaxLength && exactName.IndexOfAny(['"', '\0']) < 0
            ? $"\"{exactName}\""
            : throw OracleValidation.Argument("Invalid Oracle identifier returned from the catalogue.", nameof(exactName));

    /// <summary>Splits <c>TABLE</c> or <c>SCHEMA.TABLE</c>. A quoted name may not contain a dot.</summary>
    public static (string Schema, string Table) Split(string qualifiedName)
    {
        string[] parts = (qualifiedName ?? string.Empty).Split('.');
        return parts.Length switch
        {
            1 => (null, parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw OracleValidation.Argument(
                $"The destination table '{SafeText.Sanitize(qualifiedName)}' must be 'TABLE' or 'SCHEMA.TABLE'.", nameof(BulkWriteTarget.TableName))
        };
    }

    /// <summary>True when the name is wrapped in double quotes, which asks for an exact (case-sensitive) match.</summary>
    public static bool IsQuoted(string name) => name is { Length: > 1 } && name[0] == '"' && name[^1] == '"';

    /// <summary>Removes one pair of surrounding double quotes.</summary>
    public static string StripQuotes(string name) => IsQuoted(name) ? name[1..^1] : name;
}

using System.Globalization;
using System.Text;

namespace Loki.BulkDataProcessor.Utils.Validation;

/// <summary>Makes caller-supplied text (for example names from a JSON payload) safe to echo in an exception message.</summary>
internal static class MessageText
{
    internal const int MaxLength = 128;

    /// <summary>Replaces control, line/paragraph separator and format characters with '?' and truncates to <see cref="MaxLength"/> characters (marked with "...").</summary>
    internal static string Sanitize(string value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var truncated = value.Length > MaxLength;
        var builder = new StringBuilder(truncated ? MaxLength + 3 : value.Length);
        foreach (var character in truncated ? value.Substring(0, MaxLength) : value)
        {
            builder.Append(IsUnsafe(character) ? '?' : character);
        }

        return truncated ? builder.Append("...").ToString() : builder.ToString();
    }

    /// <summary>Control characters, U+2028/U+2029 (line breaks in some log sinks) and format characters such as U+202E
    /// (bidi override, used to spoof text in logs and UIs).</summary>
    internal static bool IsUnsafe(char character) =>
        char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Format;
}

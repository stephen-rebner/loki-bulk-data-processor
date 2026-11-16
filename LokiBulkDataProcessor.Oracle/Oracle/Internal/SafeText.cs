using System.Globalization;
using System.Text;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// Makes caller-supplied names safe to echo in an exception message: control, line/paragraph separator and format (bidi) characters become '?' and long values are
/// truncated (marked with "..."). Mirrors Core's internal MessageText, which this assembly cannot see (public SPI only).
/// </summary>
internal static class SafeText
{
    internal const int MaxLength = 128;

    internal static string Sanitize(string value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        bool truncated = value.Length > MaxLength;
        StringBuilder builder = new(truncated ? MaxLength + 3 : value.Length);
        foreach (char character in truncated ? value[..MaxLength] : value)
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

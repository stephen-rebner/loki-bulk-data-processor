using System.Text;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// Makes caller-supplied names safe to echo in an exception message: control characters become '?' and long values are
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
            builder.Append(char.IsControl(character) ? '?' : character);
        }

        return truncated ? builder.Append("...").ToString() : builder.ToString();
    }
}

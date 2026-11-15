using System.Text;

namespace Loki.BulkDataProcessor.Utils.Validation;

/// <summary>Makes caller-supplied text (for example names from a JSON payload) safe to echo in an exception message.</summary>
internal static class MessageText
{
    internal const int MaxLength = 128;

    /// <summary>Replaces control characters with '?' and truncates to <see cref="MaxLength"/> characters (marked with "...").</summary>
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
            builder.Append(char.IsControl(character) ? '?' : character);
        }

        return truncated ? builder.Append("...").ToString() : builder.ToString();
    }
}

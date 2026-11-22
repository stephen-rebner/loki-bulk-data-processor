using System;
using System.Data;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// Source-side checks shared by both writers. They run before any row is sent, and they throw marked validation exceptions
/// (<see cref="OracleValidation"/>) with fixed messages, so the caller sees the same error in either write mode.
/// </summary>
internal static class SourceColumns
{
    internal const string NoColumnsMessage = "There are no columns to copy: the source has no columns or every column was unmapped.";

    /// <summary>Throws when <paramref name="resolved"/> has no columns.</summary>
    public static void ThrowIfEmpty(OracleResolvedTarget resolved)
    {
        if (resolved.Columns.Count == 0)
        {
            throw OracleValidation.InvalidOperation(NoColumnsMessage);
        }
    }

    /// <summary>The ordinal of <paramref name="column"/> in <paramref name="source"/>, or a marked error naming the missing column.</summary>
    public static int Ordinal(IDataReader source, string column)
    {
        int ordinal;
        try
        {
            ordinal = source.GetOrdinal(column);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException)
        {
            ordinal = -1;
        }

        return ordinal >= 0 ? ordinal : throw OracleValidation.InvalidOperation(BulkCopyErrorMessages.SourceColumnNotFound(column));
    }
}

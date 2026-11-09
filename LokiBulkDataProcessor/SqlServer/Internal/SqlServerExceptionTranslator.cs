using System;
using System.Linq;
using Loki.BulkDataProcessor.Providers;

namespace Loki.BulkDataProcessor.SqlServer.Internal;

/// <summary>Maps SqlBulkCopy failures to stable messages. Always returns a new exception wrapping the original (#15).</summary>
internal static class SqlServerExceptionTranslator
{
    internal const string ColumnMappingMismatchMessage = $"{BulkCopyErrorMessages.ColumnMappingMismatch} Note the mappings are case sensitive.";

    private static readonly string[] MappingMismatchPrefixes =
    {
        "The given ColumnMapping does not match up with any column in the source or destination",
        "The given ColumnName"
    };

    public static Exception Translate(Exception exception) =>
        IsMappingMismatch(exception)
            ? new InvalidOperationException(ColumnMappingMismatchMessage, exception)
            : new InvalidOperationException(exception.Message, exception);

    private static bool IsMappingMismatch(Exception exception) =>
        exception is InvalidOperationException &&
        MappingMismatchPrefixes.Any(prefix => exception.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

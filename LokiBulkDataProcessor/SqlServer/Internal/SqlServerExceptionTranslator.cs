using System;
using System.Globalization;
using System.Linq;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Data.SqlClient;

namespace Loki.BulkDataProcessor.SqlServer.Internal;

/// <summary>
/// Maps SqlBulkCopy failures to stable, data-free messages. Always returns a new exception wrapping the original (#15).
/// Driver messages can echo row values (for example a value that failed conversion), so they are never copied into
/// the top-level message; they stay available on <see cref="Exception.InnerException"/>.
/// </summary>
internal static class SqlServerExceptionTranslator
{
    internal const string DatabaseName = "SQL Server";

    internal const string ColumnMappingMismatchMessage = $"{BulkCopyErrorMessages.ColumnMappingMismatch} Note the mappings are case sensitive.";

    private static readonly string[] MappingMismatchPrefixes =
    {
        "The given ColumnMapping does not match up with any column in the source or destination",
        "The given ColumnName"
    };

    public static Exception Translate(Exception exception, BulkWriteTarget target) =>
        IsMappingMismatch(exception)
            ? new InvalidOperationException(ColumnMappingMismatchMessage, exception)
            : new InvalidOperationException(FailureMessage(exception, target?.TableName), exception);

    private static bool IsMappingMismatch(Exception exception) =>
        exception is InvalidOperationException &&
        MappingMismatchPrefixes.Any(prefix => exception.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string FailureMessage(Exception exception, string tableName) =>
        FindSqlException(exception) is { } sqlException
            ? BulkCopyErrorMessages.BulkCopyFailed(tableName, DatabaseName, sqlException.Number.ToString(CultureInfo.InvariantCulture))
            : BulkCopyErrorMessages.BulkCopyFailed(tableName);

    /// <summary>SqlBulkCopy sometimes wraps the server error (for example a missing table), so search the chain.</summary>
    private static SqlException FindSqlException(Exception exception)
    {
        for (Exception current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }
}

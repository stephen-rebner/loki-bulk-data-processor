using System;
using System.Globalization;
using Loki.BulkDataProcessor.Providers;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// Maps Oracle write failures to fixed, data-free messages by <see cref="OracleException.Number"/>, always as a NEW exception whose
/// InnerException is the original (#15). Oracle messages can echo row values (for example ORA-12899 quotes the value that was too
/// long), so driver text is never copied into the top-level message. The table name is echoed as the caller wrote it (sanitised).
/// The provider's own validation failures are returned unchanged, so the pipeline rethrows them as they are.
/// </summary>
internal static class OracleExceptionTranslator
{
    internal const string DatabaseName = "Oracle";

    /// <summary>Added to name errors in <see cref="OracleIdentifierResolution.Unquoted"/> mode only: with CatalogLookup the names
    /// already came from the catalogue, so the advice would be wrong.</summary>
    internal const string CasingHint =
        """Unquoted Oracle names are matched case-insensitively (folded to upper case); for quoted mixed-case tables or columns use OracleIdentifierResolution.CatalogLookup.""";

    internal const string LongStringHint =
        """A string value is longer than Oracle's VARCHAR2 bind limit (4000 bytes, or 32767 with MAX_STRING_SIZE = EXTENDED); the array-bind writer cannot write longer values, also not into CLOB columns.""";

    public static Exception Translate(Exception exception, BulkWriteTarget target,
        OracleIdentifierResolution resolution = OracleIdentifierResolution.Unquoted)
    {
        if (OracleValidation.IsValidationFailure(exception))
        {
            return exception;
        }

        return FindOracleException(exception) is { } oracle
            ? FromNumber(oracle.Number, exception, target?.TableName, resolution == OracleIdentifierResolution.Unquoted)
            : new InvalidOperationException(BulkCopyErrorMessages.BulkCopyFailed(target?.TableName), exception);
    }

    /// <summary>The number-based mapping; a seam so every ORA code is unit-tested without constructing an <c>OracleException</c>.</summary>
    internal static Exception FromNumber(int number, Exception inner, string tableName, bool withCasingHint = true)
    {
        string hint = withCasingHint ? $" {CasingHint}" : string.Empty;
        return number switch
        {
            904 or 2373 => new InvalidOperationException(
                $"{BulkCopyErrorMessages.ColumnMappingMismatch} (Oracle error {ErrorCode(number)}){hint}", inner),
            942 or 39826 => new InvalidOperationException(
                $"{Failed(tableName, number)} The table does not exist or is not visible to the connected user.{hint}", inner),
            1461 => new InvalidOperationException($"{Failed(tableName, number)} {LongStringHint}", inner),
            _ => new InvalidOperationException(Failed(tableName, number), inner)
        };
    }

    /// <summary>"ORA-" plus the five-digit error number, for example ORA-00942.</summary>
    internal static string ErrorCode(int number) => $"ORA-{number.ToString("D5", CultureInfo.InvariantCulture)}";

    private static string Failed(string tableName, int number) =>
        BulkCopyErrorMessages.BulkCopyFailed(tableName, DatabaseName, ErrorCode(number));

    /// <summary>The driver may wrap the server error, so search the InnerException chain.</summary>
    private static OracleException FindOracleException(Exception exception)
    {
        for (Exception current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracleException)
            {
                return oracleException;
            }
        }

        return null;
    }
}

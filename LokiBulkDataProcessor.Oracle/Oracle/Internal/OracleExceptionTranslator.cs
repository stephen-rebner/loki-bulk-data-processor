using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        """A string value is longer than this server accepts for a VARCHAR2 bind (4000 bytes, or 32767 with MAX_STRING_SIZE = EXTENDED), also for CLOB columns; Oracle Database 23 accepts longer values for CLOB columns.""";

    internal const string TimeoutHint =
        """The command did not complete within IBulkProcessor.Timeout and was cancelled by the driver; raise the Timeout (0 means no limit) or check for locks held by other sessions.""";

    /// <summary>ORA-01013 (user requested cancel): the driver's report of an expired command timeout.</summary>
    internal const int CommandCancelled = 1013;

    /// <summary>
    /// ORA-24381 ("error(s) in array DML"): the envelope ODP.NET raises when a row of a multi-row array-bound execution fails. The
    /// row errors (ORA-00001, ORA-02291, ...) are in <see cref="OracleException.Errors"/>.
    /// </summary>
    internal const int ArrayDmlErrors = 24381;

    public static Exception Translate(Exception exception, BulkWriteTarget target,
        OracleIdentifierResolution resolution = OracleIdentifierResolution.Unquoted)
    {
        if (OracleValidation.IsValidationFailure(exception))
        {
            return exception;
        }

        return OracleErrorNumber(exception) is { } number
            ? FromNumber(number, exception, target?.TableName, resolution == OracleIdentifierResolution.Unquoted)
            : new InvalidOperationException(BulkCopyErrorMessages.BulkCopyFailed(target?.TableName), exception);
    }

    /// <summary>The ORA number from the InnerException chain; a timeout from <see cref="OracleCommandTimeout"/> is ORA-01013.</summary>
    private static int? OracleErrorNumber(Exception exception) =>
        FindOracleException(exception) is { } oracle
            ? EffectiveNumber(oracle.Number, ErrorNumbers(oracle))
            : exception is TimeoutException ? CommandCancelled : null;

    private static IEnumerable<int> ErrorNumbers(OracleException oracle) =>
        oracle.Errors?.Cast<OracleError>().Select(error => error.Number) ?? [];

    /// <summary>
    /// The number to report: for the ORA-24381 envelope, the first row error behind it (found on a real database in step 7);
    /// otherwise <paramref name="number"/>. Pure, so it is unit-tested without an <c>OracleException</c>.
    /// </summary>
    internal static int EffectiveNumber(int number, IEnumerable<int> errorNumbers) =>
        number == ArrayDmlErrors
            ? errorNumbers.Where(errorNumber => errorNumber != ArrayDmlErrors).DefaultIfEmpty(number).First()
            : number;

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
            CommandCancelled => new InvalidOperationException($"{Failed(tableName, number)} {TimeoutHint}", inner),
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

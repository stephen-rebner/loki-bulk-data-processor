using System;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// Creates the exceptions the Oracle provider throws itself, before any row reaches the server (bad identifier, unsupported
/// column type, missing source column). They are marked so <see cref="OracleExceptionTranslator"/> passes them through
/// unchanged: their messages are already fixed, data-free and more useful than a generic "bulk copy failed".
/// </summary>
internal static class OracleValidation
{
    private const string MarkerKey = "Loki.BulkDataProcessor.Oracle.ValidationFailure";

    public static ArgumentException Argument(string message, string paramName) => Mark(new ArgumentException(message, paramName));

    public static InvalidOperationException InvalidOperation(string message) => Mark(new InvalidOperationException(message));

    public static NotSupportedException NotSupported(string message) => Mark(new NotSupportedException(message));

    public static bool IsValidationFailure(Exception exception) =>
        exception is not null && exception.Data[MarkerKey] is true;

    private static TException Mark<TException>(TException exception) where TException : Exception
    {
        exception.Data[MarkerKey] = true;
        return exception;
    }
}

using System;
using System.Runtime.CompilerServices;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// Creates the exceptions the Oracle provider throws itself, before any row reaches the server (bad identifier, unsupported
/// column type, missing source column). They are marked so <see cref="OracleExceptionTranslator"/> passes them through
/// unchanged: their messages are already fixed, data-free and more useful than a generic "bulk copy failed".
/// </summary>
internal static class OracleValidation
{
    /// <summary>
    /// Identity-based and private, so no other code (a caller's reader, a model getter, a driver copying <see cref="Exception.Data"/>)
    /// can forge the pass-through, and nothing shows up in a host's structured-log dump of <see cref="Exception.Data"/>.
    /// </summary>
    private static readonly ConditionalWeakTable<Exception, object> Marked = new();

    public static ArgumentException Argument(string message, string paramName) => Mark(new ArgumentException(message, paramName));

    public static InvalidOperationException InvalidOperation(string message) => Mark(new InvalidOperationException(message));

    public static NotSupportedException NotSupported(string message) => Mark(new NotSupportedException(message));

    public static bool IsValidationFailure(Exception exception) =>
        exception is not null && Marked.TryGetValue(exception, out _);

    private static TException Mark<TException>(TException exception) where TException : Exception
    {
        Marked.AddOrUpdate(exception, null);
        return exception;
    }
}

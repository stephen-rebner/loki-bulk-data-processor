using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace Loki.Samples.WebApi.Errors;

/// <summary>
/// Recognises a unique-key violation by the driver's error number. Loki puts the driver exception in
/// <see cref="Exception.InnerException"/> (its own message is data-free); the driver message itself can quote the duplicate
/// value, so it is never returned or logged here.
/// </summary>
internal static class DuplicateKeyErrors
{
    private const int SqlServerUniqueConstraint = 2627;
    private const int SqlServerUniqueIndex = 2601;
    private const int OracleUniqueConstraint = 1;   // ORA-00001

    /// <summary>True if <paramref name="exception"/> or its InnerException is a unique-key violation.</summary>
    public static bool IsDuplicateKey(Exception? exception) =>
        IsDuplicateKeyError(exception) || IsDuplicateKeyError(exception?.InnerException);

    private static bool IsDuplicateKeyError(Exception? exception) => exception switch
    {
        SqlException sql => sql.Errors.Cast<SqlError>().Any(error => error.Number is SqlServerUniqueConstraint or SqlServerUniqueIndex),
        // A failed row of an array-bound INSERT arrives in ODP.NET's ORA-24381 envelope; its own number is in Errors.
        OracleException oracle => oracle.Number == OracleUniqueConstraint
            || oracle.Errors.Cast<OracleError>().Any(error => error.Number == OracleUniqueConstraint),
        _ => false
    };
}

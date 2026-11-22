using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>Assertions shared by the Oracle fixtures.</summary>
internal static class OracleAssertions
{
    /// <summary>
    /// Asserts the translated InvalidOperationException and that the OracleException in its chain reports <paramref name="oraNumber"/>,
    /// itself or (behind the ORA-24381 array DML envelope) in its <see cref="OracleException.Errors"/>.
    /// </summary>
    public static async Task<ExceptionAssertions<InvalidOperationException>> ThrowsTranslatedAsync(Func<Task> action, string messagePattern,
        int oraNumber)
    {
        ExceptionAssertions<InvalidOperationException> thrown =
            await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(messagePattern);
        OracleException oracle = FindOracleException(thrown.Which);
        oracle.Should().NotBeNull("the driver error is kept (#15)");
        oracle.Errors.Cast<OracleError>().Select(error => error.Number).Prepend(oracle.Number).Should().Contain(oraNumber);
        return thrown;
    }

    /// <summary>The first <see cref="OracleException"/> in the InnerException chain, or null.</summary>
    public static OracleException FindOracleException(Exception exception)
    {
        for (Exception current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracle)
            {
                return oracle;
            }
        }

        return null;
    }
}

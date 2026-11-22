using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// ODP.NET's async methods report an expired <c>OracleCommand.CommandTimeout</c> as an <see cref="OperationCanceledException"/>
/// (inner <c>OracleException</c> ORA-01013) although the caller's token was not cancelled. The pipeline passes every
/// <see cref="OperationCanceledException"/> through untranslated, so an <c>IBulkProcessor.Timeout</c> would otherwise surface as a
/// cancellation. This turns it into an <see cref="OracleCommandTimeoutException"/> (original as InnerException), which the translator
/// maps to the fixed ORA-01013 message. A cancellation the caller asked for, or any cancellation of a command without a timeout
/// (<c>CommandTimeout</c> 0), is left as it is.
/// </summary>
internal static class OracleCommandTimeout
{
    internal const string Message =
        "The Oracle command timed out or was cancelled by the driver after its timeout (IBulkProcessor.Timeout).";

    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute, int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            return await execute(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (commandTimeoutSeconds > 0 && !cancellationToken.IsCancellationRequested)
        {
            throw new OracleCommandTimeoutException(exception);
        }
    }
}

/// <summary>
/// Loki's own report of an expired command timeout (see <see cref="OracleCommandTimeout"/>). Only this type, not any
/// <see cref="TimeoutException"/>, is translated as ORA-01013 when no <c>OracleException</c> is in the chain.
/// </summary>
internal sealed class OracleCommandTimeoutException(Exception driverCancellation)
    : TimeoutException(OracleCommandTimeout.Message, driverCancellation);

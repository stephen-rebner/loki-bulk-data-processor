using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>
/// ODP.NET's async methods report an expired <c>OracleCommand.CommandTimeout</c> as an <see cref="OperationCanceledException"/>
/// (inner <c>OracleException</c> ORA-01013) although the caller's token was not cancelled. The pipeline passes every
/// <see cref="OperationCanceledException"/> through untranslated, so an <c>IBulkProcessor.Timeout</c> would otherwise surface as a
/// cancellation. This turns it into a <see cref="TimeoutException"/> (original as InnerException), which the translator maps to the
/// fixed ORA-01013 message. A cancellation the caller asked for is left as it is.
/// </summary>
internal static class OracleCommandTimeout
{
    internal const string Message = "The Oracle command did not complete within the timeout (IBulkProcessor.Timeout).";

    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute, CancellationToken cancellationToken)
    {
        try
        {
            return await execute(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(Message, exception);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>
/// ODP.NET reports an expired CommandTimeout from its async methods as an OperationCanceledException (ORA-01013) although the
/// caller's token was not cancelled; found on a real database in step 7.
/// </summary>
public class OracleCommandTimeoutTests
{
    [Test]
    public async Task RunAsync_ShouldThrowTimeoutWithTheDriverErrorAsInner_WhenTheDriverCancelsWithoutTheCallersToken()
    {
        var driverCancellation = new OperationCanceledException("ORA-01013: User requested cancel of current operation.");

        Func<Task> action = () => OracleCommandTimeout.RunAsync<int>(_ => throw driverCancellation, CancellationToken.None);

        (await action.Should().ThrowExactlyAsync<TimeoutException>().WithMessage(OracleCommandTimeout.Message))
            .Which.InnerException.Should().BeSameAs(driverCancellation);
    }

    [Test]
    public async Task RunAsync_ShouldLetTheCancellationThrough_WhenTheCallersTokenWasCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var driverCancellation = new OperationCanceledException("ORA-01013", cancellation.Token);

        Func<Task> action = () => OracleCommandTimeout.RunAsync<int>(_ => throw driverCancellation, cancellation.Token);

        (await action.Should().ThrowExactlyAsync<OperationCanceledException>()).Which.Should().BeSameAs(driverCancellation);
    }

    [Test]
    public async Task RunAsync_ShouldPassTheTokenAndReturnTheResult_WhenTheCommandCompletes()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken seen = default;

        int result = await OracleCommandTimeout.RunAsync(token => { seen = token; return Task.FromResult(42); }, cancellation.Token);

        result.Should().Be(42);
        seen.Should().Be(cancellation.Token);
    }

    [Test]
    public async Task RunAsync_ShouldRethrowOtherErrorsUnchanged_WhenTheCommandFails()
    {
        var failure = new InvalidOperationException("other");

        Func<Task> action = () => OracleCommandTimeout.RunAsync<int>(_ => throw failure, CancellationToken.None);

        (await action.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
    }
}

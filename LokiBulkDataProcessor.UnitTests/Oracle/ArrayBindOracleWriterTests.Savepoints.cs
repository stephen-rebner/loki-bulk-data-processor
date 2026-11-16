using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>
/// Every chunk is all-or-nothing (step 7 security fix): ODP.NET's batch-error mode would otherwise keep a failed chunk's valid rows in
/// the transaction. The savepoint rollback undoes only the chunk; the transaction is never ended by the writer (#12).
/// </summary>
public partial class ArrayBindOracleWriterTests
{
    [Test]
    public async Task RunInSavepointAsync_ShouldSetTheSavepointBeforeTheChunkAndNotRollBack_WhenTheChunkSucceeds()
    {
        var savepoint = new RecordingSavepoint();

        await ArrayBindOracleWriter.RunInSavepointAsync(savepoint, _ => { savepoint.Calls.Add("chunk"); return Task.CompletedTask; },
            new RecordingLoggerFactory().CreateLogger("test"), CancellationToken.None);

        savepoint.Calls.Should().Equal("set", "chunk");
    }

    [Test]
    public async Task RunInSavepointAsync_ShouldRollBackToTheSavepointAndRethrowTheChunkError_WhenTheChunkFails()
    {
        var savepoint = new RecordingSavepoint();
        var chunkError = new InvalidOperationException("ORA-24381");

        Func<Task> action = () => ArrayBindOracleWriter.RunInSavepointAsync(savepoint, _ => throw chunkError,
            new RecordingLoggerFactory().CreateLogger("test"), CancellationToken.None);

        (await action.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(chunkError);
        savepoint.Calls.Should().Equal("set", "rollback");
    }

    [Test]
    public async Task RunInSavepointAsync_ShouldRethrowTheChunkErrorAndLogOnlyTheType_WhenTheSavepointRollbackAlsoFails()
    {
        var savepoint = new RecordingSavepoint { RollbackException = new InvalidOperationException("rollback failure text") };
        var chunkError = new TimeoutException("chunk failure");
        var loggerFactory = new RecordingLoggerFactory();

        Func<Task> action = () => ArrayBindOracleWriter.RunInSavepointAsync(savepoint, _ => throw chunkError,
            loggerFactory.CreateLogger("test"), CancellationToken.None);

        (await action.Should().ThrowExactlyAsync<TimeoutException>()).Which.Should().BeSameAs(chunkError, "the rollback failure must not hide it");
        loggerFactory.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which.Should().Match<RecordingLoggerFactory.LogEntry>(
            entry => entry.Exception == null && entry.Message.Contains(typeof(InvalidOperationException).FullName)
                && !entry.Message.Contains("rollback failure text"));
    }

    [Test]
    public async Task RunInSavepointAsync_ShouldRollBackAndRethrow_WhenTheChunkIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var savepoint = new RecordingSavepoint();
        CancellationToken seen = default;

        Func<Task> action = () => ArrayBindOracleWriter.RunInSavepointAsync(savepoint, token =>
        {
            seen = token;
            cancellation.Cancel();
            throw new OperationCanceledException(token);
        }, new RecordingLoggerFactory().CreateLogger("test"), cancellation.Token);

        await action.Should().ThrowExactlyAsync<OperationCanceledException>();
        seen.Should().Be(cancellation.Token);
        savepoint.Calls.Should().Equal("set", "rollback");
    }

    [Test]
    public async Task RunInSavepointAsync_ShouldNotRunTheChunk_WhenSettingTheSavepointFails()
    {
        var savepoint = new RecordingSavepoint { SetException = new InvalidOperationException("no savepoint") };
        bool ran = false;

        Func<Task> action = () => ArrayBindOracleWriter.RunInSavepointAsync(savepoint, _ => { ran = true; return Task.CompletedTask; },
            new RecordingLoggerFactory().CreateLogger("test"), CancellationToken.None);

        await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("no savepoint");
        ran.Should().BeFalse();
    }

    [Test]
    public void ChunkSavepointName_ShouldBeAConstantSimpleIdentifier_WhenUsed()
    {
        OracleChunkSavepoint.Name.Should().MatchRegex(@"\A[A-Z][A-Z0-9_]*\z");
    }

    private sealed class RecordingSavepoint : IChunkSavepoint
    {
        public List<string> Calls { get; } = new();

        public Exception SetException { get; init; }

        public Exception RollbackException { get; init; }

        public Task SetAsync(CancellationToken cancellationToken)
        {
            Calls.Add("set");
            return SetException is null ? Task.CompletedTask : Task.FromException(SetException);
        }

        public Task RollBackAsync()
        {
            Calls.Add("rollback");
            return RollbackException is null ? Task.CompletedTask : Task.FromException(RollbackException);
        }
    }
}

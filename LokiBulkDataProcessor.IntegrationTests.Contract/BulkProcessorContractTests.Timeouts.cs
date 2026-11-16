using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// <see cref="Loki.BulkDataProcessor.IBulkProcessor.Timeout"/> and cancellation reach the driver. Another session holds an exclusive
/// lock on TestDbModels (<see cref="LockTestDbModelsTableAsync"/>) for <see cref="LockHeldFor"/> and then rolls back, so Loki's
/// INSERT waits. With the default Timeout the save then succeeds (the control); with a 1 s Timeout, or a token cancelled after 1 s,
/// it fails, Loki's own transaction is rolled back and the table stays empty.
/// </summary>
/// <remarks>
/// The lock is released on purpose rather than held until the save fails: a driver delivers a timeout or a cancellation to the
/// server as a "break", and a server or network without out-of-band breaks (for example Oracle with <c>DISABLE_OOB=ON</c>, as in the
/// gvenzl/oracle-free images) acts on it only when the current call returns. SQL Server fails after 1 s; such an Oracle server fails
/// (ORA-01013) once the lock is gone. Either way the save must fail instead of writing the rows.
/// </remarks>
public abstract partial class BulkProcessorContractTests
{
    private static readonly TimeSpan LockHeldFor = TimeSpan.FromSeconds(4);

    [Test]
    public async Task SaveAsync_ShouldSaveTheRows_WhenTheWriteWaitsForALockShorterThanTheTimeout()
    {
        await WhileTestDbModelsIsLockedAsync(() => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable));

        await TheTestDbModelValuesShouldBe("One", "Two");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowAndSaveNothing_WhenTheWriteWaitsLongerThanTheTimeout()
    {
        BulkProcessor.Timeout = 1;

        Func<Task> action = () => WhileTestDbModelsIsLockedAsync(() => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable));

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull("#15");
        await TheTestDbModelsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowOperationCanceledAndSaveNothing_WhenCancelledWhileTheWriteWaitsForALock()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        Func<Task> action = () => WhileTestDbModelsIsLockedAsync(
            () => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable, cancellation.Token));

        await action.Should().ThrowAsync<OperationCanceledException>();
        await TheTestDbModelsTableShouldBeEmpty();
    }

    /// <summary>Runs <paramref name="save"/> while another session holds the TestDbModels lock for <see cref="LockHeldFor"/>.</summary>
    private async Task WhileTestDbModelsIsLockedAsync(Func<Task> save)
    {
        await using DbConnection blocker = await OpenConnectionAsync();
        await using DbTransaction lockHolder = await blocker.BeginTransactionAsync();
        await LockTestDbModelsTableAsync(lockHolder);
        Task release = ReleaseAfterAsync(lockHolder, LockHeldFor);
        try
        {
            await save();
        }
        finally
        {
            await release;
        }
    }

    private static async Task ReleaseAfterAsync(DbTransaction lockHolder, TimeSpan delay)
    {
        await Task.Delay(delay);
        await lockHolder.RollbackAsync();
    }
}

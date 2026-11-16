using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// <see cref="Loki.BulkDataProcessor.IBulkProcessor.Timeout"/> and cancellation reach the driver. Another session holds an exclusive
/// lock on TestDbModels (<see cref="LockTestDbModelsTableAsync"/>) for <see cref="HeldTableLock.LockHeldFor"/> and then rolls back, so
/// Loki's INSERT waits. With the default Timeout the save then succeeds (the control); with a 1 s Timeout, or a token cancelled 1 s
/// after the save starts, it fails, Loki's own transaction is rolled back and the table stays empty. See <see cref="HeldTableLock"/> for
/// why the lock is released rather than held until the save fails.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
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
        using var cancellation = new CancellationTokenSource();

        Func<Task> action = () => WhileTestDbModelsIsLockedAsync(() =>
        {
            cancellation.CancelAfter(TimeSpan.FromSeconds(1));   // the clock starts once the lock is held
            return BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable, cancellation.Token);
        });

        await action.Should().ThrowAsync<OperationCanceledException>();
        await TheTestDbModelsTableShouldBeEmpty();
    }

    /// <summary>Runs <paramref name="save"/> while another session holds the TestDbModels lock (see <see cref="HeldTableLock"/>).</summary>
    private async Task WhileTestDbModelsIsLockedAsync(Func<Task> save)
    {
        await using DbConnection blocker = await OpenConnectionAsync();
        await using DbTransaction lockHolder = await blocker.BeginTransactionAsync();
        await LockTestDbModelsTableAsync(lockHolder);
        await HeldTableLock.WhileHeldAsync(lockHolder, save);
    }
}

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// The transaction rule: a caller's transaction is never committed, rolled back, disposed or closed by Loki, also when a
/// save fails (#12); settings and transactions belong to one processor instance (#11).
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldSavePostsSuccessfully_WhenCommitingExternalTransaction()
    {
        int blogId = await GivenABlogAsync();
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;

        await BulkProcessor.SaveAsync(ContractRows.Posts(blogId), PostsTable);
        await transaction.CommitAsync();

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldNotSavePosts_WhenRollingBackExternalTransaction()
    {
        int blogId = await GivenABlogAsync();
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;

        await BulkProcessor.SaveAsync(ContractRows.Posts(blogId), PostsTable);
        await transaction.RollbackAsync();

        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldLeaveTheCallersTransactionUsableAndNotRolledBack_WhenACopyInsideItFails()
    {
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;
        await BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        Func<Task> failingCopy = () => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), MissingTable);

        await failingCopy.Should().ThrowAsync<InvalidOperationException>();
        transaction.Connection.Should().BeSameAs(connection, "Loki must not roll back or complete the caller's transaction (#12)");
        connection.State.Should().Be(ConnectionState.Open);
        await transaction.CommitAsync();
        await TheTestDbModelValuesShouldBe("One", "Two");
    }

    [Test]
    public async Task SaveAsync_ShouldKeepTheCallersOwnWork_WhenACopyInsideTheCallersTransactionIsRejected()
    {
        await GivenABlogAsync();
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        await InsertBlogAsync("caller-work", transaction);
        BulkProcessor.Transaction = transaction;

        Func<Task> rejectedCopy = () => BulkProcessor.SaveAsync(ContractRows.Posts(blogId: 999_999), PostsTable);

        (await rejectedCopy.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull("#15");
        await transaction.CommitAsync();
        (await CountBlogsAsync()).Should().Be(2, "the caller's insert must survive Loki's failure (#12)");
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task FactoryProcessors_ShouldHaveIndependentTransactions_WhenOneUsesACallersTransaction()
    {
        IBulkProcessor withCallerTransaction = NewProcessor();
        IBulkProcessor withOwnTransaction = NewProcessor();
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        withCallerTransaction.Transaction = transaction;

        await withCallerTransaction.SaveAsync(new[] { ContractRows.TestDbModel(1, "rolled back") }, TestDbModelsTable);
        await transaction.RollbackAsync();
        await withOwnTransaction.SaveAsync(new[] { ContractRows.TestDbModel(2, "committed") }, TestDbModelsTable);

        withOwnTransaction.Transaction.Should().BeNull();
        await TheTestDbModelValuesShouldBe("committed");
    }

    [Test]
    public async Task FactoryProcessors_ShouldCommitAndRollBackIndependently_WhenEachHasItsOwnCallerTransaction()
    {
        int blogId = await GivenABlogAsync();
        await using DbConnection firstConnection = await OpenConnectionAsync();
        await using DbConnection secondConnection = await OpenConnectionAsync();
        await using DbTransaction committed = await firstConnection.BeginTransactionAsync();
        await using DbTransaction rolledBack = await secondConnection.BeginTransactionAsync();
        (IBulkProcessor first, IBulkProcessor second) = TwoProcessorsWithDifferentSettings(committed, rolledBack);

        await first.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        await second.SaveAsync(ContractRows.Posts(blogId), PostsTable);
        await committed.CommitAsync();
        await rolledBack.RollbackAsync();

        AssertSettingsWereNotShared(first, second, committed, rolledBack);
        await TheTestDbModelValuesShouldBe("One", "Two");
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task Transaction_ShouldFallBackToLokisOwnTransaction_WhenClearedWithNull()
    {
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;
        BulkProcessor.Transaction = null;

        await BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        await transaction.RollbackAsync();

        await TheTestDbModelValuesShouldBe("One", "Two");
    }

    private (IBulkProcessor First, IBulkProcessor Second) TwoProcessorsWithDifferentSettings(DbTransaction first, DbTransaction second)
    {
        IBulkProcessor firstProcessor = NewProcessor();
        IBulkProcessor secondProcessor = NewProcessor();
        firstProcessor.Transaction = first;
        firstProcessor.BatchSize = 1;
        secondProcessor.Transaction = second;
        secondProcessor.Timeout = 45;
        return (firstProcessor, secondProcessor);
    }

    private static void AssertSettingsWereNotShared(IBulkProcessor first, IBulkProcessor second, DbTransaction firstTransaction, DbTransaction secondTransaction)
    {
        first.Transaction.Should().BeSameAs(firstTransaction, "settings are per processor instance (#11)");
        second.Transaction.Should().BeSameAs(secondTransaction);
        first.BatchSize.Should().Be(1);
        second.BatchSize.Should().NotBe(1);
        second.Timeout.Should().Be(45);
        first.Timeout.Should().NotBe(45);
    }
}

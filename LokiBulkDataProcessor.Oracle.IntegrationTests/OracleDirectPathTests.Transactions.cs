using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The load joins the caller's OracleTransaction and is never committed by Loki; only one load per transaction (ORA-39822); a failed
/// load leaves none of its rows and keeps the caller's transaction usable; a cancelled token stops the save before the load starts.
/// </summary>
public sealed partial class OracleDirectPathTests
{
    /// <summary>How long another session's query is given to (not) complete while the load is uncommitted.</summary>
    private static readonly TimeSpan OtherSessionWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The rows are visible on the caller's connection, and the caller's rollback removes them, which it could not if Loki had committed.
    /// (No other session reads the table before the rollback: see the next test.)
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldLoadInsideTheCallersTransactionWithoutCommittingIt_WhenTheCallerSetsOne()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();

        await Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        (await CountTestDbModelsAsync(connection)).Should().Be(2, "the rows are visible inside the caller's transaction");
        transaction.Rollback();
        (await CountTestDbModelsAsync(connection)).Should().Be(0, "the caller's rollback undoes the load, so Loki did not commit it");
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Step 8 finding (not in the spike): until the caller's transaction ends, the loading session holds a library cache lock on the
    /// table, so another session's plain SELECT on it waits (event "library cache lock") instead of reading the committed rows.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldMakeOtherSessionsQueriesOnTheTableWait_UntilTheCallersTransactionEnds()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        Task<int> otherSession = Task.Run(async () => (await _database.ReadTestDbModelsAsync()).Count);
        Task first = await Task.WhenAny(otherSession, Task.Delay(OtherSessionWait));

        first.Should().NotBeSameAs(otherSession, "the query waits on the uncommitted direct-path load");
        transaction.Rollback();
        (await otherSession.WaitAsync(TimeSpan.FromSeconds(60))).Should().Be(0, "the query completes once the transaction has ended");
    }

    [Test]
    public async Task SaveAsync_ShouldBeCommittedByTheCaller_WhenTheCallerCommitsAfterTheLoad()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await InsertTestDbModelAsync(transaction, "caller");

        await Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        transaction.Commit();

        (await ReadTestDbModelStringsAsync()).Should().BeEquivalentTo("One", "Two", "caller");
    }

    /// <summary>
    /// ORA-39822 is raised before the second load writes anything; the first load's rows stay in the caller's transaction and the
    /// transaction is still usable (here: committed with the first load's rows).
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra39822MessageAndKeepTheFirstLoad_WhenASecondLoadRunsInTheSameTransaction()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        Func<Task> second = () => Processor(transaction).SaveAsync(new[] { ContractRows.TestDbModel("Three") }, TestDbModelsTable);

        await OracleAssertions.ThrowsTranslatedAsync(second,
            "Bulk copy to 'TestDbModels' failed (Oracle error ORA-39822). See InnerException for details. Only one direct-path load*", 39822);
        (await CountTestDbModelsAsync(connection)).Should().Be(2, "the first load is still in the caller's transaction");
        transaction.Commit();
        (await ReadTestDbModelStringsAsync()).Should().BeEquivalentTo("One", "Two");
    }

    /// <summary>
    /// Per-load atomicity (the step 7 follow-up): a load that fails part-way (row 2 of 3, BatchSize 1) leaves none of its rows, also
    /// rows of batches sent before the failure; the caller's earlier row stays and the transaction is still usable.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldLeaveNoneOfTheLoadsRowsAndKeepTheCallersRow_WhenALoadFailsInTheCallersTransaction()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await InsertTestDbModelAsync(transaction, "caller");
        IBulkProcessor processor = Processor(transaction);
        processor.BatchSize = 1;

        await OracleAssertions.ThrowsTranslatedAsync(() => SaveRowsWithANullInANotNullColumnAsync(processor),
            "Bulk copy to 'TestDbModels' failed (Oracle error ORA-01400). See InnerException for details.", 1400);

        (await OracleTestDatabase.QueryAsync(connection, SelectTestDbModelStrings, reader => reader.GetString(0))).Should().Equal("caller");
        transaction.Commit();
        (await ReadTestDbModelStringsAsync()).Should().Equal("caller");
    }

    /// <summary>
    /// Step 8 finding: the failed load still counts as the transaction's direct-path operation, so a retry in the same transaction
    /// fails with ORA-39822, while conventional statements keep working and the transaction commits.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldRefuseAnotherLoadButAllowOtherStatements_WhenALoadFailedInTheCallersTransaction()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        Func<Task> failed = () => SaveRowsWithANullInANotNullColumnAsync(Processor(transaction));
        await failed.Should().ThrowExactlyAsync<InvalidOperationException>();

        Func<Task> retry = () => Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        await OracleAssertions.ThrowsTranslatedAsync(retry, "Bulk copy to 'TestDbModels' failed (Oracle error ORA-39822).*", 39822);
        await InsertTestDbModelAsync(transaction, "conventional insert");
        transaction.Commit();
        (await ReadTestDbModelStringsAsync()).Should().Equal("conventional insert");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheOra39822Message_WhenTheSecondLoadInTheTransactionIsIntoAnotherTable()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await Processor(transaction).SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        Func<Task> second = () => Processor(transaction).SaveAsync(KeyedRows((1, "other table")), "KeyedRows");

        await OracleAssertions.ThrowsTranslatedAsync(second, "Bulk copy to 'KeyedRows' failed (Oracle error ORA-39822).*", 39822);
        transaction.Rollback();
    }

    [Test]
    public async Task SaveAsync_ShouldLeaveTheTableEmpty_WhenALoadFailsInLokisOwnTransaction()
    {
        IBulkProcessor processor = Processor();
        processor.BatchSize = 1;

        await OracleAssertions.ThrowsTranslatedAsync(() => SaveRowsWithANullInANotNullColumnAsync(processor),
            "Bulk copy to 'TestDbModels' failed (Oracle error ORA-01400). See InnerException for details.", 1400);

        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowOperationCanceledAndLoadNothing_WhenTheTokenIsAlreadyCancelled()
    {
        Func<Task> action = () => Processor().SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable, new CancellationToken(true));

        await action.Should().ThrowAsync<OperationCanceledException>();
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    /// <summary>Rows 1 and 3 are valid; row 2 has NULL in the NOT NULL BoolColumn.</summary>
    private static async Task SaveRowsWithANullInANotNullColumnAsync(IBulkProcessor processor)
    {
        using DataTable rows = Table(("StringColumn", typeof(string)), ("BoolColumn", typeof(bool)), ("DateColumn", typeof(DateTime)));
        rows.Rows.Add("valid 1", true, new DateTime(2026, 1, 1));
        rows.Rows.Add("null flag", DBNull.Value, new DateTime(2026, 1, 2));
        rows.Rows.Add("valid 3", false, new DateTime(2026, 1, 3));
        await processor.SaveAsync(rows, TestDbModelsTable);
    }
}

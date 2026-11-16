using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Real ORA errors and their fixed, data-free translations (the OracleException is always kept in the InnerException chain), constraint
/// enforcement and triggers on the conventional path, and timeouts/cancellation while a chunk waits on another session's lock.
/// </summary>
public sealed partial class OracleArrayBindTests
{
    private const string RowValue = "too-long-row-value";

    [Test]
    public async Task SaveAsync_ShouldThrowTheOra02291MessageAndSaveNothing_WhenAForeignKeyIsViolated()
    {
        using DataTable posts = Table(("Title", typeof(string)), ("Content", typeof(string)), ("BlogId", typeof(int)));
        posts.Rows.Add("orphan", "no blog", 999_999);

        Func<Task> action = () => Processor().SaveAsync(posts, "Posts");

        await ThrowsTranslated(action, "Bulk copy to 'Posts' failed (Oracle error ORA-02291). See InnerException for details.", 2291);
        (await _database.ReadPostsAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Each chunk is all-or-nothing in a caller's transaction (step 7 security fix). ODP.NET runs array DML in batch-error mode and
    /// would keep the failed chunk's valid row ("valid in failed chunk"). Loki rolls back to its savepoint taken before that chunk, so
    /// the row is gone. The earlier, successful chunk and the caller's own row stay, and the caller's transaction is still usable.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldUndoOnlyTheFailedChunkInTheCallersTransaction_WhenARowOfALaterChunkIsRejected()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await OracleTestDatabase.ExecuteAsync(transaction, OracleSchema.InsertKeyedRow, OracleTestDatabase.Bind("id", 1), OracleTestDatabase.Bind("name", "caller"));
        IBulkProcessor processor = Processor();
        processor.Transaction = transaction;
        processor.BatchSize = 2;

        Func<Task> action = () => processor.SaveAsync(
            KeyedRows((2, "first chunk a"), (3, "first chunk b"), (4, "valid in failed chunk"), (1, "duplicate")), "KeyedRows");

        await ThrowsTranslated(action, "Bulk copy to 'KeyedRows' failed (Oracle error ORA-00001). See InnerException for details.", 1);
        (await OracleTestDatabase.QueryAsync(connection, OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0)))
            .Should().Equal(new[] { "caller", "first chunk a", "first chunk b" }, "only the failed chunk is undone; earlier chunks stay");
        transaction.Commit();
        (await _database.QueryAsync(OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0))).Should().HaveCount(3);
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheOra00001MessageAndKeepThePrimaryKeyIndexValid_WhenAKeyIsDuplicated()
    {
        await _database.ExecuteAsync(OracleSchema.InsertKeyedRow, OracleTestDatabase.Bind("id", 1), OracleTestDatabase.Bind("name", "existing"));
        using DataTable rows = KeyedRows((2, "new"), (1, "duplicate"));

        Func<Task> action = () => Processor().SaveAsync(rows, "KeyedRows");

        await ThrowsTranslated(action, "Bulk copy to 'KeyedRows' failed (Oracle error ORA-00001). See InnerException for details.", 1);
        (await _database.QueryAsync(OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0))).Should().Equal("existing");
        (await _database.QueryAsync(OracleSchema.IndexStatus, reader => reader.GetString(0), OracleTestDatabase.Bind("indexName", OracleSchema.KeyedRowsPrimaryKey)))
            .Should().Equal(new[] { "VALID" }, "the conventional path never leaves the index UNUSABLE (unlike direct path)");
    }

    [Test]
    public async Task SaveAsync_ShouldEchoTheTableNameAsWrittenWithTheCasingHint_WhenTheTableDoesNotExist()
    {
        Func<Task> action = () => Processor().SaveAsync(NameRows("x"), "NonExistentTable");

        (await ThrowsTranslated(action, "Bulk copy to 'NonExistentTable' failed (Oracle error ORA-00942). See InnerException for details.*", 942))
            .Which.Message.Should().Contain("OracleIdentifierResolution.CatalogLookup");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheColumnMappingMessageWithOra00904_WhenADestinationColumnDoesNotExist()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("NoSuchColumn", typeof(string)));
        rows.Rows.Add("x", "y");

        Func<Task> action = () => Processor().SaveAsync(rows, OracleTypesTable);

        await ThrowsTranslated(action, $"{BulkCopyErrorMessages.ColumnMappingMismatch} (Oracle error ORA-00904)*", 904);
    }

    [Test]
    public async Task SaveAsync_ShouldKeepTheRowValueOutOfTheMessage_WhenOracleRejectsAValueAsTooLong()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("ShortText", typeof(string)));
        rows.Rows.Add("too long", RowValue);

        Func<Task> action = () => Processor().SaveAsync(rows, OracleTypesTable);

        ExceptionAssertions<InvalidOperationException> thrown =
            await ThrowsTranslated(action, "Bulk copy to 'OracleTypes' failed (Oracle error ORA-12899). See InnerException for details.", 12899);
        thrown.Which.Message.Should().NotContain(RowValue);
        (await _database.QueryAsync(SelectText, reader => reader.GetString(0))).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowNotSupportedBeforeWriting_WhenASourceColumnTypeCannotBeBound()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("TextColumn", typeof(object)));
        rows.Rows.Add("object", "value");

        Func<Task> action = () => Processor().SaveAsync(rows, OracleTypesTable);

        (await action.Should().ThrowExactlyAsync<NotSupportedException>()).Which.Message.Should().Contain("'TextColumn' has type Object");
    }

    [Test]
    public async Task SaveAsync_ShouldFireTheInsertTrigger_WhenRowsAreArrayBound()
    {
        using DataTable rows = Table(("Name", typeof(string)));
        rows.Rows.Add("first");
        rows.Rows.Add("second");

        await Processor().SaveAsync(rows, "TriggeredRows");

        (await _database.QueryAsync(OracleSchema.SelectTriggeredRows, reader => (reader.GetString(0), reader.GetString(1))))
            .Should().BeEquivalentTo(new[] { ("first", OracleSchema.TriggerStamp), ("second", OracleSchema.TriggerStamp) });
    }

    /// <summary>
    /// The step 7 fix: ODP.NET reports the expired CommandTimeout as an OperationCanceledException; the provider turns it into the
    /// translated ORA-01013 message. (The image's DISABLE_OOB=ON means the server raises it only once the lock is released.)
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra01013TimeoutMessage_WhenTheTimeoutExpiresWhileWaitingForALock()
    {
        IBulkProcessor processor = Processor();
        processor.Timeout = 1;

        Func<Task> action = () => WhileTestDbModelsIsLockedAsync(() => processor.SaveAsync(TwoTestDbModelRows(), "TestDbModels"));

        (await ThrowsTranslated(action, "Bulk copy to 'TestDbModels' failed (Oracle error ORA-01013). See InnerException for details. *", 1013))
            .Which.InnerException.Should().BeAssignableTo<TimeoutException>("Loki's own command timeout, not the driver's cancellation");
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowOperationCanceledWithOra01013Inside_WhenTheTokenIsCancelledWhileWaitingForALock()
    {
        using var cancellation = new CancellationTokenSource();

        Func<Task> action = () => WhileTestDbModelsIsLockedAsync(() =>
        {
            cancellation.CancelAfter(TimeSpan.FromSeconds(1));   // the clock starts once the lock is held
            return Processor().SaveAsync(TwoTestDbModelRows(), "TestDbModels", cancellation.Token);
        });

        (await action.Should().ThrowAsync<OperationCanceledException>()).Which.InnerException.Should().BeOfType<OracleException>()
            .Which.Number.Should().Be(1013);
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Asserts the translated InvalidOperationException and that the OracleException in its chain reports <paramref name="oraNumber"/>,
    /// itself or (behind the ORA-24381 array DML envelope) in its <see cref="OracleException.Errors"/>.
    /// </summary>
    private static async Task<ExceptionAssertions<InvalidOperationException>> ThrowsTranslated(Func<Task> action, string messagePattern, int oraNumber)
    {
        ExceptionAssertions<InvalidOperationException> thrown =
            await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(messagePattern);
        OracleException oracle = FindOracleException(thrown.Which);
        oracle.Should().NotBeNull("the driver error is kept (#15)");
        oracle.Errors.Cast<OracleError>().Select(error => error.Number).Prepend(oracle.Number).Should().Contain(oraNumber);
        return thrown;
    }

    private static OracleException FindOracleException(Exception exception)
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

    private static DataTable KeyedRows(params (int Id, string Name)[] keyedRows)
    {
        DataTable rows = Table(("Id", typeof(int)), ("Name", typeof(string)));
        foreach ((int id, string name) in keyedRows)
        {
            rows.Rows.Add(id, name);
        }

        return rows;
    }

    private static DataTable TwoTestDbModelRows()
    {
        DataTable rows = Table(("StringColumn", typeof(string)), ("BoolColumn", typeof(bool)), ("DateColumn", typeof(DateTime)));
        rows.Rows.Add("One", true, new DateTime(2026, 1, 1));
        rows.Rows.Add("Two", false, new DateTime(2026, 1, 2));
        return rows;
    }

    /// <summary>Another session holds TestDbModels (shared timing: <see cref="HeldTableLock"/>) while <paramref name="save"/> runs.</summary>
    private async Task WhileTestDbModelsIsLockedAsync(Func<Task> save)
    {
        await using OracleConnection blocker = await _database.OpenConnectionAsync();
        await using OracleTransaction lockHolder = blocker.BeginTransaction();
        await OracleTestDatabase.ExecuteAsync(lockHolder, OracleSchema.LockTestDbModels);
        await HeldTableLock.WhileHeldAsync(lockHolder, save);
    }
}

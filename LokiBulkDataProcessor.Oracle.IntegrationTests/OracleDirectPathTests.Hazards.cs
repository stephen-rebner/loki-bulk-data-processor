using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The documented direct-path hazards, characterised on the real database: tables with triggers are refused (ORA-26086), foreign keys
/// are not enforced, and duplicate primary keys are loaded without an error and leave the index UNUSABLE once committed (a rollback without
/// reading the table leaves it VALID), after which conventional inserts and deletes fail with ORA-01502 and direct-path loads with
/// ORA-26028. The fixture's reset makes the index VALID again, so no test depends on another's order.
/// </summary>
public sealed partial class OracleDirectPathTests
{
    private const string ConstraintStatus = "SELECT STATUS, VALIDATED FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = :constraintName";

    private const string PostsForeignKey = "FK_POSTS_BLOGS_BLOGID";

    private const string SelectKeyedRowById = "SELECT Name FROM KeyedRows WHERE Id = :id";

    private const string UpdateKeyedRowNames = "UPDATE KeyedRows SET Name = :name";

    private const string DeleteKeyedRowByName = "DELETE FROM KeyedRows WHERE Name = :name";

    private const string RebuildKeyedRowsIndex = "ALTER INDEX PK_KeyedRows REBUILD";

    private const string DisableKeyedRowsPrimaryKey = "ALTER TABLE KeyedRows DISABLE CONSTRAINT PK_KeyedRows";

    /// <summary>Disabling the constraint keeps the UNUSABLE index (ENABLE would then fail with ORA-14063), so it is dropped explicitly.</summary>
    private const string DropKeyedRowsIndex = "DROP INDEX PK_KeyedRows";

    private const string EnableKeyedRowsPrimaryKey = "ALTER TABLE KeyedRows ENABLE CONSTRAINT PK_KeyedRows";

    [Test]
    public async Task SaveAsync_ShouldThrowTheOra26086MessageAndSaveNothing_WhenTheTableHasATrigger()
    {
        using DataTable rows = Table(("Name", typeof(string)));
        rows.Rows.Add("first");

        Func<Task> action = () => Processor().SaveAsync(rows, "TriggeredRows");

        await OracleAssertions.ThrowsTranslatedAsync(action,
            "Bulk copy to 'TriggeredRows' failed (Oracle error ORA-26086). See InnerException for details. " +
            "Direct-path loads do not support tables with triggers*",
            26086);
        (await _database.QueryAsync(OracleSchema.SelectTriggeredRows, reader => reader.GetString(0))).Should().BeEmpty();
    }

    /// <summary>Characterisation of the hazard: the orphan is loaded and the FK still reports ENABLED / VALIDATED.</summary>
    [Test]
    public async Task SaveAsync_ShouldLoadAnOrphanPostAndLeaveTheForeignKeyValidated_WhenTheBlogDoesNotExist()
    {
        using DataTable posts = Table(("Title", typeof(string)), ("Content", typeof(string)), ("BlogId", typeof(int)));
        posts.TableName = "OrphanPosts";   // not "Posts", so PostDataMapping does not apply and the columns map by name
        posts.Rows.Add("orphan", "no blog", 999_999);

        await Processor().SaveAsync(posts, "Posts");

        (await _database.ReadPostsAsync()).Should().ContainSingle().Which.BlogId.Should().Be(999_999);
        (await _database.QueryAsync(ConstraintStatus, reader => (reader.GetString(0), reader.GetString(1)),
            OracleTestDatabase.Bind("constraintName", PostsForeignKey))).Should().Equal(("ENABLED", "VALIDATED"));
    }

    /// <summary>
    /// Characterisation of the hazard: the save succeeds with both duplicates and, once committed, PK_KEYEDROWS is UNUSABLE. (Step 8
    /// re-review: Loki cannot detect it safely inside the transaction, see DirectPathOracleWriter.)
    /// </summary>
    [TestCase(OracleProvider.Name)]
    [TestCase(CatalogProvider)]
    public async Task SaveAsync_ShouldLoadDuplicateKeysAndLeaveTheIndexUnusable_WhenAPrimaryKeyIsDuplicated(string providerName)
    {
        await Processor(providerName).SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");

        (await ReadKeyedRowNamesAsync()).Should().BeEquivalentTo("first", "duplicate");
        (await KeyedRowsIndexStatusAsync()).Should().Equal("UNUSABLE");
    }

    /// <summary>Verified: rolling back a duplicate-key load (without reading the table first) leaves the index VALID and no rows.</summary>
    [Test]
    public async Task SaveAsync_ShouldLeaveTheIndexValid_WhenTheCallerRollsBackADuplicateKeyLoad()
    {
        await using (OracleConnection connection = await _database.OpenConnectionAsync())
        await using (OracleTransaction transaction = connection.BeginTransaction())
        {
            await Processor(transaction).SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");
            transaction.Rollback();
        }

        (await ReadKeyedRowNamesAsync()).Should().BeEmpty();
        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
        await Processor(ArrayBindProvider).SaveAsync(KeyedRows((1, "after rollback")), "KeyedRows");
    }

    /// <summary>The control: unique keys load, also next to existing rows, and the index stays VALID.</summary>
    [Test]
    public async Task SaveAsync_ShouldLoadTheRows_WhenThePrimaryKeysAreUnique()
    {
        await _database.ExecuteAsync(OracleSchema.InsertKeyedRow, OracleTestDatabase.Bind("id", 1), OracleTestDatabase.Bind("name", "existing"));

        await Processor().SaveAsync(KeyedRows((2, "second"), (3, "third")), "KeyedRows");

        (await ReadKeyedRowNamesAsync()).Should().Equal("existing", "second", "third");
        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
    }

    /// <summary>
    /// After a committed duplicate-key load a conventional (array-bind) save fails with the translated ORA-01502, while queries and an
    /// UPDATE of a non-key column still work (W3).
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra01502Message_WhenADuplicateKeyLoadWasCommitted()
    {
        await CommitADuplicateKeyLoadAsync();

        (await KeyedRowsIndexStatusAsync()).Should().Equal("UNUSABLE");
        Func<Task> conventional = () => Processor(ArrayBindProvider).SaveAsync(KeyedRows((2, "later")), "KeyedRows");
        await OracleAssertions.ThrowsTranslatedAsync(conventional,
            "Bulk copy to 'KeyedRows' failed (Oracle error ORA-01502). See InnerException for details. " +
            "An index of the destination table is UNUSABLE*",
            1502);
        await _database.ExecuteAsync(UpdateKeyedRowNames, OracleTestDatabase.Bind("name", "updated"));
        (await ReadKeyedRowNamesAsync()).Should().Equal("updated", "updated");
    }

    /// <summary>
    /// With the index UNUSABLE, queries still work (Oracle skips the index), but a DELETE fails with ORA-01502 and another direct-path
    /// load with ORA-26028, which gets the same translated message as ORA-01502.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra26028Message_WhenAnotherLoadMeetsTheUnusableIndex()
    {
        await CommitADuplicateKeyLoadAsync();

        Func<Task> again = () => Processor().SaveAsync(KeyedRows((2, "again")), "KeyedRows");

        await OracleAssertions.ThrowsTranslatedAsync(again,
            "Bulk copy to 'KeyedRows' failed (Oracle error ORA-26028). See InnerException for details. " +
            "An index of the destination table is UNUSABLE*",
            26028);
        (await _database.QueryAsync(SelectKeyedRowById, reader => reader.GetString(0), OracleTestDatabase.Bind("id", 1))).Should().HaveCount(2);
        Func<Task> delete = () => _database.ExecuteAsync(DeleteKeyedRowByName, OracleTestDatabase.Bind("name", "duplicate"));
        (await delete.Should().ThrowAsync<OracleException>()).Which.Number.Should().Be(1502);
    }

    /// <summary>The repair recorded for the README, verified: ALTER INDEX ... REBUILD alone fails with ORA-01452 over duplicates.</summary>
    [Test]
    public async Task SaveAsync_ShouldWorkAgain_WhenTheConstraintIsDisabledTheDuplicatesDeletedAndTheConstraintEnabled()
    {
        await CommitADuplicateKeyLoadAsync();
        Func<Task> rebuild = () => _database.ExecuteAsync(RebuildKeyedRowsIndex);
        (await rebuild.Should().ThrowAsync<OracleException>()).Which.Number.Should().Be(1452);

        await RepairKeyedRowsPrimaryKeyAsync();

        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
        await Processor(ArrayBindProvider).SaveAsync(KeyedRows((2, "after repair")), "KeyedRows");
        (await ReadKeyedRowNamesAsync()).Should().Equal("first", "after repair");
    }

    /// <summary>
    /// Guards the test isolation the hazard tests rely on. Step 8 finding: TRUNCATE alone makes the UNUSABLE index VALID again, so the
    /// shared reset needed no extra statement.
    /// </summary>
    [Test]
    public async Task ResetAsync_ShouldRestoreThePrimaryKeyIndex_WhenADirectPathLoadLeftItUnusable()
    {
        await CommitADuplicateKeyLoadAsync();
        (await KeyedRowsIndexStatusAsync()).Should().Equal("UNUSABLE");

        await _database.ResetAsync();

        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
        await Processor(ArrayBindProvider).SaveAsync(KeyedRows((1, "after reset")), "KeyedRows");
    }

    /// <summary>A duplicate-key load committed by Loki's own transaction, which leaves PK_KEYEDROWS UNUSABLE.</summary>
    private Task CommitADuplicateKeyLoadAsync() => Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");

    private Task<IReadOnlyList<string>> ReadKeyedRowNamesAsync() =>
        _database.QueryAsync(OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0));

    private async Task RepairKeyedRowsPrimaryKeyAsync()
    {
        await _database.ExecuteAsync(DisableKeyedRowsPrimaryKey);
        await _database.ExecuteAsync(DropKeyedRowsIndex);
        await _database.ExecuteAsync(DeleteKeyedRowByName, OracleTestDatabase.Bind("name", "duplicate"));
        await _database.ExecuteAsync(EnableKeyedRowsPrimaryKey);
    }

    private Task<IReadOnlyList<string>> KeyedRowsIndexStatusAsync() =>
        _database.QueryAsync(OracleSchema.IndexStatus, reader => reader.GetString(0),
            OracleTestDatabase.Bind("indexName", OracleSchema.KeyedRowsPrimaryKey));

    private static DataTable KeyedRows(params (int Id, string Name)[] keyedRows)
    {
        DataTable rows = Table(("Id", typeof(int)), ("Name", typeof(string)));
        foreach ((int id, string name) in keyedRows)
        {
            rows.Rows.Add(id, name);
        }

        return rows;
    }
}

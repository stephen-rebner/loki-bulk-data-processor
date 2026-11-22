using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The documented direct-path hazards, characterised on the real database: tables with triggers are refused (ORA-26086), foreign keys
/// are not enforced, and duplicate primary keys are loaded and leave the index UNUSABLE, so a later conventional insert fails with
/// ORA-01502. The fixture's reset rebuilds the index, so no test depends on another's order.
/// </summary>
public sealed partial class OracleDirectPathTests
{
    private const string ConstraintStatus = "SELECT STATUS, VALIDATED FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = :constraintName";

    private const string PostsForeignKey = "FK_POSTS_BLOGS_BLOGID";

    private const string SelectKeyedRowById = "SELECT Name FROM KeyedRows WHERE Id = :id";

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
            "Bulk copy to 'TriggeredRows' failed (Oracle error ORA-26086). See InnerException for details. Direct-path loads do not support tables with triggers*",
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
    /// Characterisation of the hazard: the load succeeds with both duplicates and leaves PK_KEYEDROWS UNUSABLE; the next conventional
    /// (array-bind) save into the table fails with the translated ORA-01502 message.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldLoadDuplicateKeysAndLeaveTheIndexUnusable_WhenAPrimaryKeyIsDuplicated()
    {
        await Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");

        (await _database.QueryAsync(OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0))).Should().BeEquivalentTo("first", "duplicate");
        (await KeyedRowsIndexStatusAsync()).Should().Equal("UNUSABLE");
        Func<Task> conventional = () => Processor(ArrayBindProvider).SaveAsync(KeyedRows((2, "later")), "KeyedRows");
        await OracleAssertions.ThrowsTranslatedAsync(conventional,
            "Bulk copy to 'KeyedRows' failed (Oracle error ORA-01502). See InnerException for details. An index of the destination table is UNUSABLE*", 1502);
    }

    /// <summary>
    /// With the index UNUSABLE, queries still work (Oracle skips the index), but a DELETE fails with ORA-01502 and another direct-path
    /// load with ORA-26028, which gets the same translated message as ORA-01502.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra26028Message_WhenAnotherLoadMeetsTheUnusableIndex()
    {
        await Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");

        Func<Task> again = () => Processor().SaveAsync(KeyedRows((2, "again")), "KeyedRows");

        await OracleAssertions.ThrowsTranslatedAsync(again,
            "Bulk copy to 'KeyedRows' failed (Oracle error ORA-26028). See InnerException for details. An index of the destination table is UNUSABLE*", 26028);
        (await _database.QueryAsync(SelectKeyedRowById, reader => reader.GetString(0), OracleTestDatabase.Bind("id", 1))).Should().HaveCount(2);
        Func<Task> delete = () => _database.ExecuteAsync(DeleteKeyedRowByName, OracleTestDatabase.Bind("name", "duplicate"));
        (await delete.Should().ThrowAsync<OracleException>()).Which.Number.Should().Be(1502);
    }

    /// <summary>The repair named in the translated message, verified: ALTER INDEX ... REBUILD alone fails with ORA-01452 over duplicates.</summary>
    [Test]
    public async Task SaveAsync_ShouldWorkAgain_WhenTheConstraintIsDisabledTheDuplicatesDeletedAndTheConstraintEnabled()
    {
        await Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");
        Func<Task> rebuild = () => _database.ExecuteAsync(RebuildKeyedRowsIndex);
        (await rebuild.Should().ThrowAsync<OracleException>()).Which.Number.Should().Be(1452);

        await RepairKeyedRowsPrimaryKeyAsync();

        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
        await Processor(ArrayBindProvider).SaveAsync(KeyedRows((2, "after repair")), "KeyedRows");
        (await _database.QueryAsync(OracleSchema.SelectKeyedRowNames, reader => reader.GetString(0))).Should().Equal("first", "after repair");
    }

    /// <summary>
    /// Guards the test isolation the hazard tests rely on. Step 8 finding: TRUNCATE alone makes the UNUSABLE index VALID again, so the
    /// shared reset needed no extra statement.
    /// </summary>
    [Test]
    public async Task ResetAsync_ShouldRestoreThePrimaryKeyIndex_WhenADirectPathLoadLeftItUnusable()
    {
        await Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "KeyedRows");

        await _database.ResetAsync();

        (await KeyedRowsIndexStatusAsync()).Should().Equal("VALID");
        await Processor(ArrayBindProvider).SaveAsync(KeyedRows((1, "after reset")), "KeyedRows");
    }

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

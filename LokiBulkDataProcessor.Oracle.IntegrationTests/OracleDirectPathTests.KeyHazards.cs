using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Step 8 security re-review findings, characterised on Oracle 23.26: a DEFERRABLE primary key with duplicates is silently DISABLED at
/// commit; an index that is already UNUSABLE makes the load fail with ORA-26028; and reading the loaded table in the transaction before a
/// rollback makes the next direct-path load return the rolled-back rows (why Loki does not probe the indexes after a load), which
/// <c>TRUNCATE ... DROP ALL STORAGE</c> clears (the fixture's reset uses it).
/// </summary>
public sealed partial class OracleDirectPathTests
{
    private const string ConstraintStatusByName =
        "SELECT STATUS || '/' || VALIDATED FROM USER_CONSTRAINTS WHERE CONSTRAINT_NAME = :constraintName";

    /// <summary>A full scan, so the loaded table blocks are read (an index-only read triggered the hazard only intermittently).</summary>
    private const string CountKeyedRowsByFullScan = "SELECT /*+ FULL(t) */ COUNT(*) FROM KeyedRows t";

    private const string TruncateKeyedRowsDroppingStorage = "TRUNCATE TABLE KeyedRows DROP ALL STORAGE";

    private const string MakePostsBlogIdIndexUnusable = "ALTER INDEX IX_Posts_BlogId UNUSABLE";

    private const string RebuildPostsBlogIdIndex = "ALTER INDEX IX_Posts_BlogId REBUILD";

    /// <summary>The DEFERRABLE primary key is backed by a NONUNIQUE index: the duplicates are kept and the constraint DISABLED, no error.</summary>
    [Test]
    public async Task SaveAsync_ShouldKeepTheDuplicatesAndDisableThePrimaryKey_WhenADeferrablePrimaryKeyIsDuplicated()
    {
        await Processor().SaveAsync(KeyedRows((1, "first"), (1, "duplicate")), "DeferrableKeyedRows");

        (await _database.QueryAsync(OracleSchema.SelectDeferrableKeyedRowNames, reader => reader.GetString(0))).Should().HaveCount(2);
        (await _database.QueryAsync(ConstraintStatusByName, reader => reader.GetString(0),
            OracleTestDatabase.Bind("constraintName", OracleSchema.DeferrableKeyedRowsPrimaryKey))).Should().Equal("DISABLED/NOT VALIDATED");
    }

    /// <summary>An index that is UNUSABLE before the save (here a non-unique one) makes the load itself fail with ORA-26028.</summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheOra26028Message_WhenANonUniqueIndexWasUnusableBeforeTheSave()
    {
        int blogId = await _database.InsertBlogAsync("http://a-url.com");
        await _database.ExecuteAsync(MakePostsBlogIdIndexUnusable);
        try
        {
            Func<Task> action = () => Processor().SaveAsync(ContractRows.Posts(blogId, count: 1), "Posts");

            await OracleAssertions.ThrowsTranslatedAsync(action, "Bulk copy to 'Posts' failed (Oracle error ORA-26028).*", 26028);
        }
        finally
        {
            await _database.ExecuteAsync(RebuildPostsBlogIdIndex);
        }
    }

    /// <summary>
    /// Characterises an Oracle behaviour (seen in 20 of 20 rounds with a full-scan read while writing the re-review): the caller reads the loaded table, rolls
    /// back, and the next direct-path load stores its rows but queries return the rolled-back rows. If an Oracle update fixes it, this
    /// test fails: then revisit the post-load index check that was dropped because of it.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldReturnTheRolledBackRows_WhenTheLoadedTableWasReadBeforeTheRollback()
    {
        await LoadReadAndRollBackAsync();

        await Processor().SaveAsync(KeyedRows((5, "five"), (6, "six")), "KeyedRows");

        (await ReadKeyedRowNamesAsync()).Should().BeEquivalentTo(new[] { "rolled back 1", "rolled back 2" }, "the stale blocks are returned");
    }

    [Test]
    public async Task SaveAsync_ShouldReturnItsOwnRows_WhenTheTableIsTruncatedDroppingAllStorageAfterTheRollback()
    {
        await LoadReadAndRollBackAsync();
        await _database.ExecuteAsync(TruncateKeyedRowsDroppingStorage);

        await Processor().SaveAsync(KeyedRows((5, "five"), (6, "six")), "KeyedRows");

        (await ReadKeyedRowNamesAsync()).Should().Equal("five", "six");
    }

    /// <summary>A direct-path load in the caller's transaction, a full scan of the table in it, then the caller's rollback.</summary>
    private async Task LoadReadAndRollBackAsync()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        await Processor(transaction).SaveAsync(KeyedRows((1, "rolled back 1"), (2, "rolled back 2")), "KeyedRows");
        IReadOnlyList<int> count = await OracleTestDatabase.QueryAsync(connection, CountKeyedRowsByFullScan, reader => Convert.ToInt32(reader.GetValue(0)));
        count.Should().Equal(2);
        transaction.Rollback();
    }
}

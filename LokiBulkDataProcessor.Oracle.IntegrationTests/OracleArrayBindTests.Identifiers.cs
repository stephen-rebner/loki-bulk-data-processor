using System;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.Oracle;
using NUnit.Framework;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Destination name resolution on a real catalogue: Unquoted (fold to upper case, no round trip) and CatalogLookup (both
/// ALL_TAB_COLUMNS queries, with their repeated named binds), plus schema-qualified and hostile names.
/// </summary>
public sealed partial class OracleArrayBindTests
{
    private const string HostileTableName = "TestDbModels; DROP TABLE Blogs--";

    [Test]
    public async Task SaveAsync_ShouldThrowTheMissingTableMessageWithTheCasingHint_WhenAMixedCaseTableIsWrittenUnquoted()
    {
        Func<Task> action = () => Processor().SaveAsync(NameRows("unquoted"), OracleSchema.MixedCaseTable);

        (await ThrowsTranslated(action, "Bulk copy to 'MixedCase' failed (Oracle error ORA-00942). See InnerException for details.*", 942))
            .Which.Message.Should().Contain("OracleIdentifierResolution.CatalogLookup");
    }

    [TestCase("\"MixedCase\"", "Name", Description = "exact query: quoted names as stored")]
    [TestCase("mixedcase", "name", Description = "case-insensitive fallback query: unquoted request, mixed-case table and column")]
    public async Task SaveAsync_ShouldWriteToTheMixedCaseTable_WhenCatalogLookupResolvesTheName(string tableName, string column)
    {
        using DataTable rows = Table((column, typeof(string)));
        rows.Rows.Add("resolved");

        await Processor(CatalogProvider).SaveAsync(rows, tableName);

        (await _database.QueryAsync(OracleSchema.SelectMixedCaseNames, reader => reader.GetString(0))).Should().Equal("resolved");
    }

    [TestCase(CatalogProvider, "Posts", Description = "exact query: UPPER(:tableName) finds POSTS")]
    [TestCase(CatalogProvider, "loki_test.Posts", Description = "exact query with an owner")]
    [TestCase(CatalogProvider, "\"LOKI_TEST\".\"POSTS\"", Description = "quoted owner and table")]
    [TestCase(OracleProvider.Name, "loki_test.Posts", Description = "Unquoted: folded to \"LOKI_TEST\".\"POSTS\"")]
    public async Task SaveAsync_ShouldWriteThePosts_WhenTheDestinationResolvesToPosts(string providerName, string tableName)
    {
        int blogId = await _database.InsertBlogAsync("http://a-url.com");

        await Processor(providerName).SaveAsync(PostRows(blogId), tableName);

        (await _database.ReadPostsAsync()).Should().ContainSingle().Which.Title.Should().Be("resolved post");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowNotFoundWithTheCallersName_WhenCatalogLookupCannotFindTheTable()
    {
        Func<Task> action = () => Processor(CatalogProvider).SaveAsync(NameRows("x"), "NoSuchTable");

        await action.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("The destination table 'NoSuchTable' was not found or is not visible to the connected user.");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheColumnMappingMessage_WhenCatalogLookupCannotFindAColumn()
    {
        using DataTable rows = Table(("NoSuchColumn", typeof(string)));
        rows.Rows.Add("x");

        Func<Task> action = () => Processor(CatalogProvider).SaveAsync(rows, "\"MixedCase\"");

        await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch} ('NoSuchColumn' not found)");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowArgumentExceptionBeforeAnySql_WhenAHostileTableNameIsWrittenUnquoted()
    {
        await _database.InsertBlogAsync("http://a-url.com");

        Func<Task> action = () => Processor().SaveAsync(TwoTestDbModelRows(), HostileTableName);

        (await action.Should().ThrowExactlyAsync<ArgumentException>()).Which.ParamName.Should().Be("destinationTableName");
        (await _database.CountBlogsAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_ShouldOnlyBindTheHostileNameAndFindNothing_WhenAHostileTableNameIsLookedUpInTheCatalog()
    {
        await _database.InsertBlogAsync("http://a-url.com");

        Func<Task> action = () => Processor(CatalogProvider).SaveAsync(TwoTestDbModelRows(), HostileTableName);

        await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage("The destination table * was not found*");
        (await _database.CountBlogsAsync()).Should().Be(1);
    }

    private static DataTable NameRows(string name)
    {
        DataTable rows = Table(("Name", typeof(string)));
        rows.Rows.Add(name);
        return rows;
    }

    private static DataTable PostRows(int blogId)
    {
        DataTable rows = Table(("Title", typeof(string)), ("Content", typeof(string)), ("BlogId", typeof(int)));
        rows.Rows.Add("resolved post", "content", blogId);
        return rows;
    }
}

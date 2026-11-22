using System;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using NUnit.Framework;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The resolvers' quoted names reach <c>OracleBulkCopy.DestinationTableName</c> / <c>DestinationSchemaName</c> and the column mappings
/// (Unquoted <c>Posts</c> and <c>loki_test.Posts</c>, CatalogLookup <c>"MixedCase"</c>); hostile names and missing source columns fail
/// before the load; a missing table or column gets the same translated messages as array bind.
/// </summary>
public sealed partial class OracleDirectPathTests
{
    [TestCase(OracleProvider.Name, "Posts", Description = "Unquoted: \"POSTS\"")]
    [TestCase(OracleProvider.Name, "loki_test.Posts", Description = "Unquoted: \"LOKI_TEST\".\"POSTS\"")]
    [TestCase(CatalogProvider, "loki_test.Posts", Description = "CatalogLookup: exact query with an owner")]
    public async Task SaveAsync_ShouldLoadThePosts_WhenTheDestinationResolvesToPosts(string providerName, string tableName)
    {
        int blogId = await _database.InsertBlogAsync("http://a-url.com");

        await Processor(providerName).SaveAsync(ContractRows.Posts(blogId, count: 1), tableName);

        (await _database.ReadPostsAsync()).Should().ContainSingle().Which.Title.Should().Be("Post1");
    }

    [TestCase("\"MixedCase\"", "Name", Description = "exact query: quoted names as stored")]
    [TestCase("mixedcase", "name", Description = "case-insensitive fallback query")]
    public async Task SaveAsync_ShouldLoadIntoTheMixedCaseTable_WhenCatalogLookupResolvesTheName(string tableName, string column)
    {
        using DataTable rows = Table((column, typeof(string)));
        rows.Rows.Add("resolved");

        await Processor(CatalogProvider).SaveAsync(rows, tableName);

        (await _database.QueryAsync(OracleSchema.SelectMixedCaseNames, reader => reader.GetString(0))).Should().Equal("resolved");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowArgumentExceptionBeforeAnySql_WhenAHostileTableNameIsWrittenUnquoted()
    {
        await _database.InsertBlogAsync("http://a-url.com");

        Func<Task> action = () => Processor().SaveAsync(ContractRows.TwoTestDbModels(), HostileIdentifiers.TableName);

        (await action.Should().ThrowExactlyAsync<ArgumentException>()).Which.ParamName.Should().Be("destinationTableName");
        (await _database.CountBlogsAsync()).Should().Be(1);
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty();
    }

    /// <summary>PostImportDataMapping maps ImportTitle/ImportContent/ImportBlogId; this reader has Title/Content/BlogId.</summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheSourceColumnNotFoundMessageBeforeTheLoad_WhenAMappedSourceColumnIsMissing()
    {
        int blogId = await _database.InsertBlogAsync("http://a-url.com");
        using IDataReader reader = ContractRows.PostReader("Title", "Content", "BlogId", blogId, "unmapped");

        Func<Task> action = () => Processor().SaveAsync(reader, "Posts", PostImportDataMapping.SourceName);

        await action.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(BulkCopyErrorMessages.SourceColumnNotFound("ImportTitle"));
        (await _database.ReadPostsAsync()).Should().BeEmpty();
    }

    /// <summary>OracleBulkCopy reports a missing table as ORA-39826 (array bind: ORA-00942); both get the same message.</summary>
    [Test]
    public async Task SaveAsync_ShouldEchoTheTableNameAsWrittenWithTheCasingHint_WhenTheTableDoesNotExist()
    {
        using DataTable rows = Table(("Name", typeof(string)));
        rows.Rows.Add("x");

        Func<Task> action = () => Processor().SaveAsync(rows, "NonExistentTable");

        (await OracleAssertions.ThrowsTranslatedAsync(action, "Bulk copy to 'NonExistentTable' failed (Oracle error ORA-39826). See InnerException for details. The table does not exist*", 39826))
            .Which.Message.Should().Contain("OracleIdentifierResolution.CatalogLookup");
    }

    /// <summary>OracleBulkCopy reports a missing destination column as ORA-02373 (array bind: ORA-00904).</summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheColumnMappingMessage_WhenADestinationColumnDoesNotExist()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("NoSuchColumn", typeof(string)));
        rows.Rows.Add("x", "y");

        Func<Task> action = () => Processor().SaveAsync(rows, "OracleTypes");

        await OracleAssertions.ThrowsTranslatedAsync(action, $"{BulkCopyErrorMessages.ColumnMappingMismatch} (Oracle error ORA-02373)*", 2373);
    }
}

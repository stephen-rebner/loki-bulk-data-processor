using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastMember;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.SqlServer;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.Mappings;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests;

/// <summary>
/// 9.0.0 step 4 fixes against a real SQL Server: #11 settings isolation, #12 caller transaction left alone on failure,
/// #13 no connection leak, #15 InnerException kept, #16 one DataMapping lookup rule. Step 5 moves the provider-neutral
/// ones into the shared contract suite.
/// </summary>
public class BulkProcessorIssueFixTests : BaseIntegrationTest
{
    private const string DocumentedMappingMessage =
        "The given column mappings does not match up with any column in the destination table. Note the mappings are case sensitive.";

    [Test]
    public async Task SaveAsync_ShouldLeaveTheCallersTransactionUsableAndNotRolledBack_WhenACopyInsideItFails()
    {
        await using SqlConnection connection = await OpenExternalConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;
        await BulkProcessor.SaveAsync(TwoTestDbModels(), nameof(TestDbContext.TestDbModels));

        Func<Task> failingCopy = () => BulkProcessor.SaveAsync(TwoTestDbModels(), "NonExistentTable");

        await failingCopy.Should().ThrowAsync<InvalidOperationException>();
        transaction.Connection.Should().BeSameAs(connection, "Loki must not roll back or complete the caller's transaction (#12)");
        connection.State.Should().Be(ConnectionState.Open);
        await transaction.CommitAsync();
        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_ShouldLeaveTheTableEmpty_WhenACopyWithItsOwnTransactionFails()
    {
        var blog = GivenABlog();
        var posts = PostsTable(BadDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blog.Id);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, nameof(TestDbContext.Posts));

        await action.Should().ThrowAsync<InvalidOperationException>();
        await TheDatabaseTableShouldBeEmpty<Post>();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheDocumentedMessageWithTheDriverErrorAsInner_WhenAMappedDestinationColumnDoesNotExist()
    {
        var blog = GivenABlog();
        var posts = PostsTable(BadDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blog.Id);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, nameof(TestDbContext.Posts));

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>().WithMessage(DocumentedMappingMessage);
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("'NoSuchColumn' does not match up", "the driver's own error is kept as InnerException (#15)");
    }

    [Test]
    public async Task SaveAsync_ShouldKeepTheSqlExceptionAsInner_WhenTheDatabaseRejectsTheRows()
    {
        var postsForAMissingBlog = PostsTable("UnmappedPosts", "Title", "Content", "BlogId", blogId: 999);

        Func<Task> action = () => BulkProcessor.SaveAsync(postsForAMissingBlog, nameof(TestDbContext.Posts));

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().Be(547, "a FOREIGN KEY violation");
        thrown.Which.Message.Should().Be("Bulk copy to 'Posts' failed (SQL Server error 547). See InnerException for details.");
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDestinationTablesDataMapping_WhenTheDataTableHasNoName()
    {
        var blog = GivenABlog();
        var unnamedPosts = PostsTable(tableName: "", "ATitle", "ContentA", "ABlogId", blog.Id);

        await BulkProcessor.SaveAsync(unnamedPosts, nameof(TestDbContext.Posts));

        (await TestDbContext.Posts.Select(post => post.Title).ToListAsync()).Should().BeEquivalentTo("Post1", "Post2");
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDataMappingForTheSourceName_WhenAReaderIsSavedWithASourceName()
    {
        var blog = GivenABlog();
        using DataTableReader reader = PostsTable("", "ImportTitle", "ImportContent", "ImportBlogId", blog.Id).CreateDataReader();

        await BulkProcessor.SaveAsync(reader, nameof(TestDbContext.Posts), PostImportDataMapping.SourceName);

        (await TestDbContext.Posts.Select(post => post.Content).ToListAsync()).Should().BeEquivalentTo("Content1", "Content2");
        reader.IsClosed.Should().BeFalse("Loki does not dispose a caller's reader");
    }

    [Test]
    public async Task FactoryProcessors_ShouldHaveIndependentTransactions_WhenOneUsesACallersTransaction()
    {
        IBulkProcessor withCallerTransaction = BulkProcessorFactory.Create(SqlServerProvider.Name, GetConnectionString());
        IBulkProcessor withOwnTransaction = BulkProcessorFactory.Create(SqlServerProvider.Name, GetConnectionString());
        await using SqlConnection connection = await OpenExternalConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        withCallerTransaction.Transaction = transaction;

        await withCallerTransaction.SaveAsync(new[] { TestDbModel(1, "rolled back") }, nameof(TestDbContext.TestDbModels));
        await transaction.RollbackAsync();
        await withOwnTransaction.SaveAsync(new[] { TestDbModel(2, "committed") }, nameof(TestDbContext.TestDbModels));

        withOwnTransaction.Transaction.Should().BeNull();
        (await TestDbContext.TestDbModels.Select(model => model.StringColumn).ToListAsync()).Should().Equal("committed");
    }

    [Test]
    public async Task Transaction_ShouldFallBackToLokisOwnTransaction_WhenClearedWithNull()
    {
        await using SqlConnection connection = await OpenExternalConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;
        BulkProcessor.Transaction = null;

        await BulkProcessor.SaveAsync(TwoTestDbModels(), nameof(TestDbContext.TestDbModels));
        await transaction.RollbackAsync();

        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_ShouldNotLeakConnections_WhenCopiesFailRepeatedly()
    {
        var connectionString = new SqlConnectionStringBuilder(GetConnectionString()) { MaxPoolSize = 2, ConnectTimeout = 5 }.ToString();
        IBulkProcessor processor = BulkProcessorFactory.Create(SqlServerProvider.Name, connectionString);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Func<Task> failingCopy = () => processor.SaveAsync(TwoTestDbModels(), "NonExistentTable");
            await failingCopy.Should().ThrowAsync<InvalidOperationException>().WithMessage("*NonExistentTable*");
        }

        await processor.SaveAsync(TwoTestDbModels(), nameof(TestDbContext.TestDbModels));
        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_ShouldThrowOperationCanceledAndSaveNothing_WhenTheTokenIsAlreadyCancelled()
    {
        Func<Task> action = () => BulkProcessor.SaveAsync(TwoTestDbModels(), nameof(TestDbContext.TestDbModels), new CancellationToken(canceled: true));

        await action.Should().ThrowAsync<OperationCanceledException>();
        await TheDatabaseTableShouldBeEmpty<TestDbModel>();
    }

    [Test]
    public async Task SaveAsync_ShouldSaveEveryRow_WhenTheBatchSizeIsSmallerThanTheRowCount()
    {
        BulkProcessor.BatchSize = 2;
        var models = Enumerable.Range(1, 5).Select(id => TestDbModel(id, $"Row {id}")).ToArray();

        await BulkProcessor.SaveAsync(models, nameof(TestDbContext.TestDbModels));

        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(5);
    }

    [Test]
    public async Task SaveAsync_ShouldSkipDeletedRows_WhenTheDataTableContainsAPendingDelete()
    {
        var table = new TestDataTableBuilder()
            .WithRowData(1, "kept", true, new DateTime(2020, 01, 26), null, null)
            .WithRowData(2, "deleted", false, new DateTime(2020, 01, 27), null, null)
            .Build();
        table.AcceptChanges();
        table.Rows[1].Delete();

        await BulkProcessor.SaveAsync(table, nameof(TestDbContext.TestDbModels));

        (await TestDbContext.TestDbModels.Select(model => model.StringColumn).ToListAsync()).Should().Equal("kept");
    }

    [Test]
    public async Task SaveAsync_ShouldMapByName_WhenTheSourceNameHasNoMappingEvenThoughTheDestinationHasOne()
    {
        var blog = GivenABlog();
        var posts = new[] { new { Title = "T1", Content = "C1", BlogId = blog.Id } };
        using var reader = ObjectReader.Create(posts, "Title", "Content", "BlogId");
        IBulkProcessor processor = BulkProcessorFactory.Create("sqlserver", GetConnectionString());

        await processor.SaveAsync(reader, nameof(TestDbContext.Posts), sourceName: "UnmappedPosts");

        (await TestDbContext.Posts.Select(post => post.Title).ToListAsync()).Should().Equal("T1");
    }

    private Blog GivenABlog()
    {
        var blog = TestObjectFactory.NewBlog().WithUrl("http://a-url.com").Build();
        SaveEntities(blog);
        return blog;
    }

    private static DataTable PostsTable(string tableName, string titleColumn, string contentColumn, string blogIdColumn, int blogId) =>
        TestObjectFactory.NewPostDataTable()
            .Create()
            .WithTableName(tableName)
            .WithCustomColumnNames(titleColumn, contentColumn, blogIdColumn)
            .WithRowData(blogId, "Post1", "Content1")
            .WithRowData(blogId, "Post2", "Content2")
            .Build();

    private static TestDbModel[] TwoTestDbModels() => new[] { TestDbModel(1, "One"), TestDbModel(2, "Two") };

    private static TestDbModel TestDbModel(int id, string value) =>
        TestObjectFactory.TestDbModelObject()
            .WithId(id)
            .WithStringColumnValue(value)
            .WithDateColumnValue(new DateTime(2020, 01, 26))
            .WithBoolColumnValue(true)
            .Build();
}

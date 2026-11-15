using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests;

/// <summary>
/// SQL Server-specific behaviour that is not part of the provider contract: SqlBulkCopy's own error texts and numbers,
/// DataTableReader's handling of deleted rows, and the Microsoft.Data.SqlClient transaction type check.
/// The provider-neutral tests run in <see cref="SqlServerContractTests"/>.
/// </summary>
public class BulkProcessorSqlServerTests : BaseIntegrationTest
{
    private const string DocumentedMappingMessage =
        "The given column mappings does not match up with any column in the destination table. Note the mappings are case sensitive.";

    [Test]
    public async Task SaveAsync_ShouldThrowTheDocumentedMessageWithTheDriverErrorAsInner_WhenAMappedDestinationColumnDoesNotExist()
    {
        var blog = GivenABlog();
        using var posts = ContractRows.PostsDataTable(BadDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blog.Id);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, nameof(TestDbContext.Posts));

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>().WithMessage(DocumentedMappingMessage);
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("'NoSuchColumn' does not match up", "SqlBulkCopy's own error is kept as InnerException (#15)");
    }

    [Test]
    public async Task SaveAsync_ShouldKeepTheSqlExceptionAsInner_WhenTheDatabaseRejectsTheRows()
    {
        using var postsForAMissingBlog = ContractRows.PostsDataTable("UnmappedPosts", "Title", "Content", "BlogId", blogId: 999);

        Func<Task> action = () => BulkProcessor.SaveAsync(postsForAMissingBlog, nameof(TestDbContext.Posts));

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().Be(547, "a FOREIGN KEY violation");
        thrown.Which.Message.Should().Be("Bulk copy to 'Posts' failed (SQL Server error 547). See InnerException for details.");
    }

    [Test]
    public async Task SaveAsync_ShouldSkipDeletedRows_WhenTheDataTableContainsAPendingDelete()
    {
        using var table = ContractRows.TestDbModelsDataTable(ContractRows.TestDbModel("kept"), ContractRows.TestDbModel("deleted"));
        table.AcceptChanges();
        table.Rows[1].Delete();

        await BulkProcessor.SaveAsync(table, nameof(TestDbContext.TestDbModels));

        (await TestDbContext.TestDbModels.Select(model => model.StringColumn).ToListAsync()).Should().Equal("kept");
    }

    [Test]
    public async Task Transaction_ShouldThrowArgumentExceptionAndLeaveTheTransactionAlone_WhenItIsNotASqlClientTransaction()
    {
        var foreignTransaction = new ForeignDbTransaction();

        Action action = () => BulkProcessor.Transaction = foreignTransaction;

        action.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("transaction");
        BulkProcessor.Transaction.Should().BeNull();
        foreignTransaction.WasTouched.Should().BeFalse("Loki never commits, rolls back or disposes a caller's transaction");
        await BulkProcessor.SaveAsync(new[] { ContractRows.TestDbModel("own transaction") }, nameof(TestDbContext.TestDbModels));
        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(1);
    }

    private Blog GivenABlog()
    {
        var blog = new Blog { Url = "http://a-url.com" };
        SaveEntities(blog);
        return blog;
    }

    /// <summary>A transaction from a driver other than Microsoft.Data.SqlClient; records any call Loki makes on it.</summary>
    private sealed class ForeignDbTransaction : DbTransaction
    {
        public bool WasTouched { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => null;

        public override void Commit() => WasTouched = true;

        public override void Rollback() => WasTouched = true;

        protected override void Dispose(bool disposing) => WasTouched = true;
    }
}

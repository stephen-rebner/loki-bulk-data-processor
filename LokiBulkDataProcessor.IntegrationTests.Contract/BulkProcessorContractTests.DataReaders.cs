using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using FastMember;
using Loki.BulkDataProcessor;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>Saving an IDataReader, with and without a DataMapping, and inside a caller's transaction.</summary>
public abstract partial class BulkProcessorContractTests
{
    private static readonly string[] MappedPostColumns = { "ATitle", "ContentA", "ABlogId" };
    private static readonly string[] UnmappedPostColumns = { "Title", "Content", "BlogId" };

    [Test]
    public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingAMapping()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.PostDtos(blogId), MappedPostColumns);

        await BulkProcessor.SaveAsync(reader, PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingNotAMapping()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.UnmappedPostDtos(blogId), UnmappedPostColumns);

        await NewProcessorWithoutMappings().SaveAsync(reader, PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingAMappingAndATransaction()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.PostDtos(blogId), MappedPostColumns);

        await SaveInsideACallerTransactionAsync(BulkProcessor, reader, commit: true);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldNotSavePosts_WhenTheTransactionIsRolledBack()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.PostDtos(blogId), MappedPostColumns);

        await SaveInsideACallerTransactionAsync(BulkProcessor, reader, commit: false);

        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingNotAMappingAndUsingATransaction()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.UnmappedPostDtos(blogId), UnmappedPostColumns);

        await SaveInsideACallerTransactionAsync(NewProcessorWithoutMappings(), reader, commit: true);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldNotSaveUnmappedReaderRows_WhenTheTransactionIsRolledBack()
    {
        int blogId = await GivenABlogAsync();
        using IDataReader reader = ObjectReader.Create(ContractRows.UnmappedPostDtos(blogId), UnmappedPostColumns);

        await SaveInsideACallerTransactionAsync(NewProcessorWithoutMappings(), reader, commit: false);

        await ThePostsTableShouldBeEmpty();
    }

    private async Task SaveInsideACallerTransactionAsync(IBulkProcessor processor, IDataReader reader, bool commit)
    {
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        processor.Transaction = transaction;

        await processor.SaveAsync(reader, PostsTable);

        await CompleteAsync(transaction, commit);
    }

    private static Task CompleteAsync(DbTransaction transaction, bool commit) =>
        commit ? transaction.CommitAsync() : transaction.RollbackAsync();
}

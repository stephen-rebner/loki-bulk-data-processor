using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Failures: Loki's own transaction is rolled back (nothing saved), the documented message keeps the driver error as
/// InnerException (#15), and failed saves release their connections (#13). Only provider-neutral message parts are asserted.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldLeaveTheTableEmpty_WhenACopyWithItsOwnTransactionFails()
    {
        int blogId = await GivenABlogAsync();
        using DataTable posts = ContractRows.PostsDataTable(BadDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blogId);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, PostsTable);

        await action.Should().ThrowAsync<InvalidOperationException>();
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldLeaveTheTableEmpty_WhenALaterBatchIsRejected()
    {
        int blogId = await GivenABlogAsync();
        Post[] validThenInvalid = ContractRows.Posts(blogId, count: 4).Append(ContractRows.Posts(blogId: 999_999, count: 1)[0]).ToArray();
        BulkProcessor.BatchSize = 1;

        Func<Task> action = () => BulkProcessor.SaveAsync(validThenInvalid, PostsTable);

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull("#15");
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheDocumentedMessageWithTheDriverErrorAsInner_WhenAMappedDestinationColumnDoesNotExist()
    {
        int blogId = await GivenABlogAsync();
        using DataTable posts = ContractRows.PostsDataTable(BadDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blogId);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, PostsTable);

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>().WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch}*");
        thrown.Which.InnerException.Should().NotBeNull("the driver's own error is kept as InnerException (#15)");
    }

    [Test]
    public async Task SaveAsync_ShouldNotLeakConnections_WhenCopiesFailRepeatedly()
    {
        IBulkProcessor processor = NewProcessor(WithMaxPoolSize(ConnectionString, 2));

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Func<Task> failingCopy = () => processor.SaveAsync(ContractRows.TwoTestDbModels(), MissingTable);
            await failingCopy.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{MissingTable}*");
        }

        await processor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        await TheTestDbModelValuesShouldBe("One", "Two");
    }
}

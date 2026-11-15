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
    /// <summary>
    /// Loki's own transaction is rolled back after rows have already reached the server: four batches of one valid post,
    /// then a post whose BlogId has no blog (a FOREIGN KEY violation, the only constraint it breaks: no identity column is
    /// written). The control save proves the four valid rows load on their own, so the rejection came from the last row.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldLeaveTheTableEmpty_WhenALaterBatchIsRejected()
    {
        int blogId = await GivenABlogAsync();
        Post[] valid = ContractRows.Posts(blogId, count: 4);
        BulkProcessor.BatchSize = 1;

        Func<Task> action = () => BulkProcessor.SaveAsync(valid.Append(ContractRows.Posts(blogId: 999_999, count: 1)[0]), PostsTable);

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.InnerException.Should().NotBeNull("#15");
        await ThePostsTableShouldBeEmpty();
        await BulkProcessor.SaveAsync(valid, PostsTable);
        await ThePostTitlesShouldBe("Post1", "Post2", "Post3", "Post4");
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

using System.Data;
using System.Threading.Tasks;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>Saving a DataTable, with and without a DataMapping.</summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasNoForeignKey()
    {
        TestDbModel[] rows = ContractRows.TwoMixedNullTestDbModels();
        using DataTable dataTable = ContractRows.TestDbModelsDataTable(rows);

        await BulkProcessor.SaveAsync(dataTable, TestDbModelsTable);

        await TheTestDbModelsShouldBe(rows);
    }

    [Test]
    public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasAForeignKey_AndHasNoMapping()
    {
        int blogId = await GivenABlogAsync();
        // Named so no DataMapping matches: an unnamed table written to Posts would use PostDataMapping (#16).
        using DataTable posts = ContractRows.PostsDataTable("UnmappedPosts", "Title", "Content", "BlogId", blogId);

        await BulkProcessor.SaveAsync(posts, PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Content1"), (blogId, "Post2", "Content2"));
    }

    [Test]
    public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasAForeignKey_AndHasAMapping()
    {
        int blogId = await GivenABlogAsync();
        using DataTable posts = ContractRows.PostsDataTable("Posts", "ATitle", "ContentA", "ABlogId", blogId);

        await BulkProcessor.SaveAsync(posts, PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Content1"), (blogId, "Post2", "Content2"));
    }
}

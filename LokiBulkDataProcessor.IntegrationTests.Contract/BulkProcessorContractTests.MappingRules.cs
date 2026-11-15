using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FastMember;
using FluentAssertions;
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// The one DataMapping lookup rule (#16): the source name (DataTable.TableName or the reader's sourceName) is the key,
/// else the destination table name. JSON has no source name, so its key is the destination.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldUseTheDestinationTablesDataMapping_WhenTheDataTableHasNoName()
    {
        int blogId = await GivenABlogAsync();
        using DataTable unnamedPosts = ContractRows.PostsDataTable(tableName: "", "ATitle", "ContentA", "ABlogId", blogId);

        await BulkProcessor.SaveAsync(unnamedPosts, PostsTable);

        await ThePostTitlesShouldBe("Post1", "Post2");
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDataMappingForTheSourceName_WhenAReaderIsSavedWithASourceName()
    {
        int blogId = await GivenABlogAsync();
        using DataTable source = ContractRows.PostsDataTable("", "ImportTitle", "ImportContent", "ImportBlogId", blogId);
        using DataTableReader reader = source.CreateDataReader();

        await BulkProcessor.SaveAsync(reader, PostsTable, PostImportDataMapping.SourceName);

        (await ReadPostsAsync()).Select(post => post.Content).Should().BeEquivalentTo("Content1", "Content2");
        reader.IsClosed.Should().BeFalse("Loki does not dispose a caller's reader");
    }

    [Test]
    public async Task SaveAsync_ShouldMapByName_WhenTheSourceNameHasNoMappingEvenThoughTheDestinationHasOne()
    {
        int blogId = await GivenABlogAsync();
        var posts = new[] { new { Title = "T1", Content = "C1", BlogId = blogId } };
        using IDataReader reader = ObjectReader.Create(posts, "Title", "Content", "BlogId");

        await BulkProcessor.SaveAsync(reader, PostsTable, sourceName: "UnmappedPosts");

        await ThePostTitlesShouldBe("T1");
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheSameDataMapping_WhenEveryInputShapeResolvesToTheSameKey()
    {
        int blogId = await GivenABlogAsync();

        await SaveOnePostThroughEveryShapeAsync(blogId);

        await ThePostTitlesShouldBe("named table", "unnamed table", "reader with source", "reader without source", "json");
    }

    private async Task SaveOnePostThroughEveryShapeAsync(int blogId)
    {
        using DataTable named = MappedPostTable("Posts", blogId, "named table");
        using DataTable unnamed = MappedPostTable("", blogId, "unnamed table");
        using IDataReader withSource = ContractRows.PostReader("ATitle", "ContentA", "ABlogId", blogId, "reader with source");
        using IDataReader withoutSource = ContractRows.PostReader("ATitle", "ContentA", "ABlogId", blogId, "reader without source");
        using Stream json = ContractRows.MappedPostsJson("PayloadNameIsIgnored", blogId, "json");

        await BulkProcessor.SaveAsync(named, PostsTable);
        await BulkProcessor.SaveAsync(unnamed, PostsTable);
        await BulkProcessor.SaveAsync(withSource, PostsTable, sourceName: "Posts");
        await BulkProcessor.SaveAsync(withoutSource, PostsTable);
        await BulkProcessor.SaveAsync(json, PostsTable);
    }

    /// <summary>One post with PostDataMapping's source columns; it only loads if that mapping (key "Posts") is used.</summary>
    private static DataTable MappedPostTable(string tableName, int blogId, string title)
    {
        DataTable table = ContractRows.PostsDataTable(tableName, "ATitle", "ContentA", "ABlogId", blogId);
        table.Rows[0]["ATitle"] = title;
        table.Rows.RemoveAt(1);
        return table;
    }
}

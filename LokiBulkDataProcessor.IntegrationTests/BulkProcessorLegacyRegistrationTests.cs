using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests;

/// <summary>
/// Smoke tests for the 8.x registration <c>AddLokiBulkDataProcessor(connectionString, mappingAssembly)</c> (SQL Server only).
/// <see cref="BaseIntegrationTest.BulkProcessor"/> is the scoped processor from that registration with the contract mappings.
/// </summary>
public class BulkProcessorLegacyRegistrationTests : BaseIntegrationTest
{
    [Test]
    public async Task AddLokiBulkDataProcessor_ShouldMapColumnsByName_WhenTheMappingAssemblyIsNull()
    {
        Blog blog = GivenABlog();
        // Named "Posts": with the contract mappings PostDataMapping (ATitle, ContentA, ABlogId) would apply and this would fail.
        using DataTable posts = ContractRows.PostsDataTable("Posts", "Title", "Content", "BlogId", blog.Id);
        await using ServiceProvider services = new ServiceCollection()
            .AddLokiBulkDataProcessor(GetConnectionString(), mappingAssembly: null)
            .BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IBulkProcessor>().SaveAsync(posts, nameof(TestDbContext.Posts));

        (await PostTitlesAsync()).Should().BeEquivalentTo("Post1", "Post2");
    }

    [Test]
    public async Task SaveAsync_ShouldRollBackWithTheCallersTransaction_WhenTheProcessorComesFromTheLegacyRegistration()
    {
        Blog blog = GivenABlog();
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        BulkProcessor.Transaction = transaction;

        await BulkProcessor.SaveAsync(ContractRows.PostDtos(blog.Id), nameof(TestDbContext.Posts));   // PostDtoMapping
        int postsInsideTheTransaction = await CountPostsAsync(connection, transaction);
        await transaction.RollbackAsync();

        postsInsideTheTransaction.Should().Be(2, "the mapped rows were written inside the caller's transaction");
        connection.State.Should().Be(ConnectionState.Open, "Loki never closes the caller's connection");
        await TheDatabaseTableShouldBeEmpty<Post>();
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDataMappingFromTheMappingAssembly_WhenJsonIsSavedThroughTheLegacyRegistration()
    {
        Blog blog = GivenABlog();
        // ATitle/ContentA/ABlogId only load through PostDataMapping, registered from the mapping assembly.
        using Stream json = ContractRows.MappedPostsJson("PayloadNameIsIgnored", blog.Id, "Legacy JSON");

        await BulkProcessor.SaveAsync(json, nameof(TestDbContext.Posts));

        (await PostTitlesAsync()).Should().Equal("Legacy JSON");
    }

    private Blog GivenABlog()
    {
        var blog = new Blog { Url = "http://a-url.com" };
        SaveEntities(blog);
        return blog;
    }

    private Task<List<string>> PostTitlesAsync() =>
        TestDbContext.Posts.AsNoTracking().Select(post => post.Title).ToListAsync();

    private static async Task<int> CountPostsAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM Posts", connection, transaction);
        return (int)await command.ExecuteScalarAsync();
    }
}

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.SqlServer;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using LokiBulkDataProcessor.IntegrationTests.EF;
using LokiBulkDataProcessor.IntegrationTests.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests;

/// <summary>
/// Runs the provider-neutral contract suite against SQL Server: one container for the fixture, the database dropped and
/// re-created with the EF Core migrations before each test.
/// </summary>
[TestFixture]
public sealed class SqlServerContractTests : BulkProcessorContractTests
{
    private readonly SqlServerTestDatabase _database = new();

    protected override string ProviderName => SqlServerProvider.Name;

    protected override string ConnectionString => _database.ConnectionString;

    protected override void AddProvider(ILokiBulkDataProcessorBuilder builder) => builder.AddSqlServer();

    protected override Task StartDatabaseAsync() => _database.StartAsync();

    protected override Task StopDatabaseAsync() => _database.DisposeAsync().AsTask();

    protected override Task ResetDataAsync() => _database.ResetAsync();

    protected override async Task<DbConnection> OpenConnectionAsync() => await _database.OpenConnectionAsync();

    protected override async Task<int> InsertBlogAsync(string url, DbTransaction transaction = null)
    {
        if (transaction is null)
        {
            await using SqlConnection connection = await _database.OpenConnectionAsync();
            return await InsertBlogAsync(connection, null, url);
        }

        return await InsertBlogAsync((SqlConnection)transaction.Connection, (SqlTransaction)transaction, url);
    }

    protected override async Task<int> CountBlogsAsync()
    {
        await using TestDbContext context = _database.CreateDbContext();
        return await context.Blogs.CountAsync();
    }

    protected override async Task<IReadOnlyList<PostRow>> ReadPostsAsync()
    {
        await using TestDbContext context = _database.CreateDbContext();
        return await context.Posts.AsNoTracking().OrderBy(post => post.Id)
            .Select(post => new PostRow(post.Id, post.BlogId, post.Title, post.Content))
            .ToListAsync();
    }

    protected override async Task<IReadOnlyList<TestDbModel>> ReadTestDbModelsAsync()
    {
        await using TestDbContext context = _database.CreateDbContext();
        return await context.TestDbModels.AsNoTracking().OrderBy(model => model.Id).ToListAsync();
    }

    protected override string WithMaxPoolSize(string connectionString, int maxPoolSize) =>
        new SqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize, ConnectTimeout = 5 }.ToString();

    private static async Task<int> InsertBlogAsync(SqlConnection connection, SqlTransaction transaction, string url)
    {
        await using var command = new SqlCommand("INSERT INTO Blogs (Url) OUTPUT INSERTED.Id VALUES (@url)", connection, transaction);
        command.Parameters.AddWithValue("@url", url);
        return (int)await command.ExecuteScalarAsync();
    }
}

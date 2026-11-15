using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.SqlServer;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.Mappings;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests;

/// <summary>
/// Step 4 security-review fixes against a real SQL Server: the caller chooses the JSON destination, the opt-in
/// RequireDataMappingForJson and MaxJsonPayloadBytes options.
/// </summary>
public class BulkProcessorJsonSecurityTests : BaseIntegrationTest
{
    [Test]
    public async Task SaveAsync_ShouldWriteToTheCallersDestination_WhenTheJsonPayloadNamesAnotherTable()
    {
        using Stream json = TestDbModelsJson(payloadTableName: "NonExistentTable");

        await BulkProcessor.SaveAsync(json, nameof(TestDbContext.TestDbModels));

        (await TestDbContext.TestDbModels.OrderBy(model => model.Id).Select(model => model.StringColumn).ToListAsync())
            .Should().Equal("One", "Two");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowAndSaveNothing_WhenAMappingIsRequiredAndTheJsonDestinationHasNone()
    {
        using ServiceProvider services = BuildServices(options => options.RequireDataMappingForJson = true);
        IBulkProcessor processor = CreateProcessor(services);
        using Stream json = TestDbModelsJson(payloadTableName: nameof(TestDbContext.TestDbModels));

        Func<Task> action = () => processor.SaveAsync(json, nameof(TestDbContext.TestDbModels));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RequireDataMappingForJson*");
        await TheDatabaseTableShouldBeEmpty<TestDbModel>();
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDestinationsDataMapping_WhenAMappingIsRequiredAndOneIsRegistered()
    {
        using ServiceProvider services = BuildServices(options => options.RequireDataMappingForJson = true);
        IBulkProcessor processor = CreateProcessor(services);
        Blog blog = GivenABlog();
        using Stream json = PostsJson(blog.Id, payloadTableName: "SomethingElse");

        await processor.SaveAsync(json, nameof(TestDbContext.Posts));

        (await TestDbContext.Posts.Select(post => post.Title).ToListAsync()).Should().Equal("Mapped title");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowInvalidDataExceptionAndSaveNothing_WhenTheJsonPayloadIsOverTheLimit()
    {
        using ServiceProvider services = BuildServices(options => options.MaxJsonPayloadBytes = 64);
        IBulkProcessor processor = CreateProcessor(services);
        using Stream json = TestDbModelsJson(payloadTableName: nameof(TestDbContext.TestDbModels));

        Func<Task> action = () => processor.SaveAsync(json, nameof(TestDbContext.TestDbModels));

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*MaxJsonPayloadBytes limit of 64 bytes*");
        await TheDatabaseTableShouldBeEmpty<TestDbModel>();
    }

    [Test]
    public async Task SaveAsync_ShouldSaveTheJson_WhenThePayloadIsWithinTheLimit()
    {
        using ServiceProvider services = BuildServices(options => options.MaxJsonPayloadBytes = 1024 * 1024);
        IBulkProcessor processor = CreateProcessor(services);
        using Stream json = TestDbModelsJson(payloadTableName: nameof(TestDbContext.TestDbModels));

        await processor.SaveAsync(json);

        (await TestDbContext.TestDbModels.CountAsync()).Should().Be(2);
    }

    private static ServiceProvider BuildServices(Action<LokiBulkDataProcessorOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(options =>
        {
            options.AddMappingsFrom(typeof(PostDataMapping).Assembly);
            configure(options);
        }).AddSqlServer();
        return services.BuildServiceProvider();
    }

    private IBulkProcessor CreateProcessor(ServiceProvider services) =>
        services.GetRequiredService<IBulkProcessorFactory>().Create(SqlServerProvider.Name, GetConnectionString());

    private Blog GivenABlog()
    {
        var blog = TestObjectFactory.NewBlog().WithUrl("http://a-url.com").Build();
        SaveEntities(blog);
        return blog;
    }

    private static Stream TestDbModelsJson(string payloadTableName) => Utf8($$"""
        {
          "tableName": "{{payloadTableName}}",
          "columns": [
            { "name": "Id", "type": "int" }, { "name": "StringColumn", "type": "string" },
            { "name": "DateColumn", "type": "datetime" }, { "name": "BoolColumn", "type": "bool" }
          ],
          "data": [
            { "Id": 1, "StringColumn": "One", "DateColumn": "2020-01-26T00:00:00", "BoolColumn": true },
            { "Id": 2, "StringColumn": "Two", "DateColumn": "2020-01-27T00:00:00", "BoolColumn": false }
          ]
        }
        """);

    /// <summary>Uses PostDataMapping's source columns (ATitle, ContentA, ABlogId), so it only loads through that mapping.</summary>
    private static Stream PostsJson(int blogId, string payloadTableName) => Utf8($$"""
        {
          "tableName": "{{payloadTableName}}",
          "columns": [ { "name": "ATitle", "type": "string" }, { "name": "ContentA", "type": "string" }, { "name": "ABlogId", "type": "int" } ],
          "data": [ { "ATitle": "Mapped title", "ContentA": "Mapped content", "ABlogId": {{blogId}} } ]
        }
        """);

    private static Stream Utf8(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));
}

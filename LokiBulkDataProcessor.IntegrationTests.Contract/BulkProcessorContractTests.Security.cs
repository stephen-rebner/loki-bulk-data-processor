using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestLogging;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Hostile identifiers (CWE-89) and connection-string secrets (CWE-200/209/532). A provider may reject a hostile table name before
/// any SQL runs (<see cref="ArgumentException"/>) or report it as a failed copy (<see cref="InvalidOperationException"/>), so the
/// table-name test asserts only that the save throws and that nothing was touched. A hostile destination column is the documented
/// column-mapping error on every provider.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    private const string HostileTableName = "TestDbModels; DROP TABLE Blogs--";

    [Test]
    public async Task SaveAsync_ShouldThrowAndLeaveEveryTableUntouched_WhenTheDestinationTableNameIsHostile()
    {
        await GivenABlogAsync();

        Func<Task> action = () => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), HostileTableName);

        await action.Should().ThrowAsync<Exception>();
        (await CountBlogsAsync()).Should().Be(1, "the injected DROP TABLE must never run");
        await TheTestDbModelsTableShouldBeEmpty();
    }

    [TestCase("Title\"; DROP TABLE Blogs--")]
    [TestCase("Title]; DROP TABLE Blogs--")]
    [TestCase("Title;--")]
    public async Task SaveAsync_ShouldThrowTheColumnMappingErrorAndLeaveEveryTableUntouched_WhenAJsonColumnNameIsHostile(string hostileColumn)
    {
        int blogId = await GivenABlogAsync();
        using Stream json = PostsJsonWithTitleColumn(hostileColumn, blogId);

        Func<Task> action = () => NewProcessorWithoutMappings().SaveAsync(json, PostsTable);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch}*");
        (await CountBlogsAsync()).Should().Be(1, "the injected DROP TABLE must never run");
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheColumnMappingErrorAndLeaveEveryTableUntouched_WhenAMappedDestinationColumnIsHostile()
    {
        int blogId = await GivenABlogAsync();
        using DataTable posts = ContractRows.PostsDataTable(HostileDestinationDataMapping.SourceName, "Title", "Content", "BlogId", blogId);

        Func<Task> action = () => BulkProcessor.SaveAsync(posts, PostsTable);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch}*");
        (await CountBlogsAsync()).Should().Be(1, "the injected DROP TABLE must never run");
        await ThePostsTableShouldBeEmpty();
    }

    [Test]
    public async Task BulkProcessor_ShouldNeverLogOrThrowTheConnectionStringSecrets_WhenSavesSucceedOrFail()
    {
        var loggerFactory = new TestLoggerFactory();
        await using ServiceProvider services = BuildServices(options => options.LoggerFactory = loggerFactory);
        IBulkProcessor processor = CreateProcessor(services);

        await processor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        Exception missingTable = await FailureOf(() => processor.SaveAsync(ContractRows.TwoTestDbModels(), MissingTable));
        Exception rejectedRow = await FailureOf(() => processor.SaveAsync(ContractRows.Posts(blogId: 999_999, count: 1), PostsTable));

        loggerFactory.Entries.Should().NotBeEmpty("the test must observe Loki's log output to be meaningful");
        ConnectionStringSecrets.Should().NotBeEmpty().And.OnlyContain(secret => !string.IsNullOrEmpty(secret));
        foreach (string secret in ConnectionStringSecrets)
        {
            TheSecretShouldNotBeExposed(secret, loggerFactory, processor, missingTable, rejectedRow);
        }
    }

    private static void TheSecretShouldNotBeExposed(string secret, TestLoggerFactory loggerFactory, IBulkProcessor processor,
        params Exception[] failures)
    {
        loggerFactory.AnyEntryContains(secret).Should().BeFalse("connection-string secrets must never be logged");
        processor.ToString().Should().NotContain(secret);
        foreach (Exception failure in failures)
        {
            failure.Message.Should().NotContain(secret);
            failure.ToString().Should().NotContain(secret, "neither the message nor any InnerException may carry a secret");
        }
    }

    private static async Task<Exception> FailureOf(Func<Task> save) => (await save.Should().ThrowAsync<Exception>()).Which;

    /// <summary>One post whose title column has the given (hostile) name; the other columns match Posts by name.</summary>
    private static Stream PostsJsonWithTitleColumn(string titleColumn, int blogId)
    {
        var payload = new
        {
            tableName = "Posts",
            columns = new[]
            {
                new { name = titleColumn, type = "string" },
                new { name = "Content", type = "string" },
                new { name = "BlogId", type = "int" }
            },
            data = new[] { new Dictionary<string, object> { [titleColumn] = "T", ["Content"] = "C", ["BlogId"] = blogId } }
        };
        return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(payload));
    }
}

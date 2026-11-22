using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Specialized;
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
    private const string WrongPassword = "Wrong_Pwd_9z";

    /// <summary>Nothing listens on the discard port of the loopback address, so the connection is refused at once.</summary>
    private const string UnreachableHost = "127.0.0.1";

    private const int UnreachablePort = 9;

    [Test]
    public async Task SaveAsync_ShouldThrowAndLeaveEveryTableUntouched_WhenTheDestinationTableNameIsHostile()
    {
        await GivenABlogAsync();

        Func<Task> action = () => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), HostileIdentifiers.TableName);

        await ThrowsArgumentOrInvalidOperation(action);
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
        // With the contract mappings, so the Post save reaches the database and fails on the FOREIGN KEY (not on an unmapped property).
        await using ServiceProvider services = BuildServices(options => { AddContractMappings(options); options.LoggerFactory = loggerFactory; });
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

    /// <summary>A wrong password: the fixed open-failure message, and no password (wrong or real) in Message or ToString().</summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheFixedOpenFailureMessageWithoutSecrets_WhenThePasswordIsWrong()
    {
        IBulkProcessor processor = NewProcessor(WithPassword(ConnectionString, WrongPassword));

        Exception failure = await OpenFailureOf(processor);

        TheOpenFailureShouldNotExpose(failure, ConnectionStringSecrets.Append(WrongPassword));
    }

    /// <summary>
    /// An unreachable server: the fixed open-failure message, which therefore names neither the host nor the port, and no password in
    /// Message or ToString(). ToString() may name the host and port: it includes the driver's own error (#15), which on both providers
    /// can describe the endpoint it could not reach, so only the Message is held to that.
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldThrowTheFixedOpenFailureMessageWithoutSecrets_WhenTheServerIsUnreachable()
    {
        IBulkProcessor processor = NewProcessor(WithServer(ConnectionString, UnreachableHost, UnreachablePort));

        Exception failure = await OpenFailureOf(processor);

        TheOpenFailureShouldNotExpose(failure, ConnectionStringSecrets);
        failure.Message.Should().NotContain(UnreachableHost);
    }

    private async Task<Exception> OpenFailureOf(IBulkProcessor processor)
    {
        Func<Task> action = () => processor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);
        ExceptionAssertions<InvalidOperationException> thrown = await action.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"Could not open a connection or begin a transaction for provider '{ProviderName}'. See InnerException for details.");
        thrown.Which.InnerException.Should().NotBeNull("the driver's error is kept (#15)");
        return thrown.Which;
    }

    private static void TheOpenFailureShouldNotExpose(Exception failure, IEnumerable<string> secrets)
    {
        foreach (string secret in secrets)
        {
            failure.Message.Should().NotContain(secret);
            failure.ToString().Should().NotContain(secret, "neither the message nor any InnerException may carry a password");
        }
    }

    /// <summary>A failure must be one of the two documented types: ArgumentException (rejected before any SQL) or InvalidOperationException.</summary>
    private static async Task<Exception> ThrowsArgumentOrInvalidOperation(Func<Task> action)
    {
        Exception thrown = (await action.Should().ThrowAsync<Exception>()).Which;
        thrown.Should().Match<Exception>(exception => exception is ArgumentException || exception is InvalidOperationException,
            "a failed save is reported as ArgumentException or InvalidOperationException, never as a raw driver exception");
        return thrown;
    }

    private static Task<Exception> FailureOf(Func<Task> save) => ThrowsArgumentOrInvalidOperation(save);

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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer.Internal;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using LokiBulkDataProcessor.UnitTests.TestModels;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests;

/// <summary>Security-review fixes for 9.0.0 step 4: data-free errors and logs, hidden connection string,
/// log forging, caller-chosen JSON destination, opt-in JSON mapping requirement and payload limits.</summary>
public class BulkProcessorSecurityTests
{
    private const string ConnectionString = "Server=fake;Password=secret";
    private const string RowValue = "secret-row-value";
    private const string Destination = "Posts";
    private static readonly ValidModelObject[] ModelObjects = { new ValidModelObject() };

    private FakeProvider _provider;
    private RecordingLoggerFactory _loggerFactory;

    [SetUp]
    public void SetUp()
    {
        _provider = new FakeProvider("FakeDb");
        _loggerFactory = new RecordingLoggerFactory();
    }

    [TestCase(true, TestName = "SaveAsync_ShouldKeepRowValuesOutOfTheMessageAndLogs_WhenTheSqlServerTranslatorHandlesTheError")]
    [TestCase(false, TestName = "SaveAsync_ShouldKeepRowValuesOutOfTheMessageAndLogs_WhenTheProviderTranslationDropsTheOriginal")]
    public async Task SaveAsync_ShouldKeepRowValuesOutOfTheMessageAndLogs_WhenTheDriverMessageContainsThem(bool useSqlServerTranslator)
    {
        var driverError = new InvalidOperationException(
            $"The given value '{RowValue}' of type String from the data source cannot be converted to type int of the specified target column.");
        _provider.WriteException = driverError;
        GivenTheProviderTranslatesWith(useSqlServerTranslator);

        Func<Task> action = () => CreateProcessor().SaveAsync(ModelObjects, Destination);

        var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().NotContain(RowValue);
        thrown.Which.InnerException.Should().BeSameAs(driverError, "the driver error is kept for the caller (#15)");
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
        _loggerFactory.Entries.Should().NotContain(entry => LogEntryContains(entry, RowValue));
    }

    [Test]
    public async Task SaveAsync_ShouldLogTheFailureWithoutTheExceptionObject_WhenTheWriteFails()
    {
        _provider.WriteException = new InvalidOperationException(RowValue);

        Func<Task> action = () => CreateProcessor().SaveAsync(ModelObjects, Destination);

        await action.Should().ThrowAsync<InvalidOperationException>();
        RecordingLoggerFactory.LogEntry error = _loggerFactory.Entries.Single(entry => entry.Level == LogLevel.Error);
        error.Exception.Should().BeNull();
        error.Message.Should().Contain("System.InvalidOperationException");
    }

    [Test]
    public void BulkProcessor_ShouldHideEveryConnectionStringFieldFromTheDebugger_WhenCreatedWithAConnectionString()
    {
        BulkProcessor processor = CreateProcessor();

        FieldInfo[] holders = FieldsHolding(processor, ConnectionString);

        holders.Should().NotBeEmpty();
        holders.Should().OnlyContain(field =>
            field.GetCustomAttribute<DebuggerBrowsableAttribute>() != null &&
            field.GetCustomAttribute<DebuggerBrowsableAttribute>().State == DebuggerBrowsableState.Never);
    }

    [Test]
    public void BulkProcessor_ShouldShowOnlyTheProviderName_WhenViewedInTheDebugger()
    {
        typeof(BulkProcessor).GetCustomAttribute<DebuggerDisplayAttribute>().Value.Should().Be("ProviderName = {ProviderName}");
    }

    [Test]
    public void ToString_ShouldNotContainTheConnectionString_WhenTheProcessorHoldsOne()
    {
        CreateProcessor().ToString().Should().Be("BulkProcessor (FakeDb)").And.NotContain("secret").And.NotContain("Server");
    }

    [Test]
    public async Task SaveAsync_ShouldLogASanitisedTableName_WhenTheDestinationContainsCrLf()
    {
        await CreateProcessor().SaveAsync(ModelObjects, "Posts\r\nFORGED log line");

        _loggerFactory.Contains(LogLevel.Information, "Starting bulk copy to Posts??FORGED log line via FakeDb").Should().BeTrue();
        _loggerFactory.Entries.Should().NotContain(entry => entry.Message.Contains('\r') || entry.Message.Contains('\n'));
    }

    [Test]
    public async Task SaveAsync_ShouldLogASanitisedTableName_WhenTheJsonPayloadTableNameContainsCrLf()
    {
        await CreateProcessor().SaveAsync(Json(@"TableB\r\nFORGED"));

        _loggerFactory.Contains(LogLevel.Information, "for table TableB??FORGED").Should().BeTrue();
        _loggerFactory.Contains(LogLevel.Information, "Starting bulk copy to TableB??FORGED").Should().BeTrue();
        _loggerFactory.Entries.Should().NotContain(entry => entry.Message.Contains('\r') || entry.Message.Contains('\n'));
    }

    [Test]
    public async Task SaveAsync_ShouldWriteToTheCallersDestinationWithItsMapping_WhenAJsonDestinationIsGiven()
    {
        await CreateProcessor().SaveAsync(Json("ValidDataMapping1"), "TableB");

        BulkWriteTarget target = _provider.Targets.Single();
        target.TableName.Should().Be("TableB", "the payload's tableName does not choose the destination");
        target.Columns.Should().Equal(new ColumnMap("BaseInt", "base_int"), new ColumnMap("AnotherColumn", "another_column"));
    }

    [Test]
    public async Task SaveAsync_ShouldMapJsonColumnsByName_WhenNoMappingIsRequiredOrRegistered()
    {
        await CreateProcessor().SaveAsync(Json("Unmapped"), "AlsoUnmapped");

        _provider.Targets.Single().Columns.Should().Equal(new ColumnMap("BaseInt", "BaseInt"), new ColumnMap("AnotherColumn", "AnotherColumn"));
    }

    [Test]
    public async Task SaveAsync_ShouldThrowBeforeConnectingAndDisposeTheJsonReader_WhenAMappingIsRequiredButNoneMatches()
    {
        IBulkProcessor processor = CreateProcessor(new JsonPayloadPolicy(RequireDataMapping: true, MaxPayloadBytes: null));

        Func<Task> legacy = () => processor.SaveAsync(Json("Unmapped"));
        Func<Task> withDestination = () => processor.SaveAsync(Json("TableB"), "Unmapped");

        await legacy.Should().ThrowAsync<InvalidOperationException>().WithMessage("No DataMapping is registered for the JSON destination 'Unmapped'*");
        await withDestination.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RequireDataMappingForJson*");
        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
        _loggerFactory.Entries.Count(entry => entry.Message == "Disposing JsonDataReader").Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_ShouldWrite_WhenAMappingIsRequiredAndOneMatches()
    {
        IBulkProcessor processor = CreateProcessor(new JsonPayloadPolicy(RequireDataMapping: true, MaxPayloadBytes: null));

        await processor.SaveAsync(Json("Ignored"), "TableB");

        _provider.Targets.Single().TableName.Should().Be("TableB");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowInvalidDataExceptionBeforeConnecting_WhenASeekableJsonPayloadIsOverTheLimit()
    {
        IBulkProcessor processor = CreateProcessor(new JsonPayloadPolicy(RequireDataMapping: false, MaxPayloadBytes: 16));

        Func<Task> action = () => processor.SaveAsync(Json("TableB"));

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*MaxJsonPayloadBytes limit of 16 bytes*");
        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task SaveAsync_ShouldStopReadingAtTheLimit_WhenANonSeekableJsonPayloadIsOverTheLimit()
    {
        IBulkProcessor processor = CreateProcessor(new JsonPayloadPolicy(RequireDataMapping: false, MaxPayloadBytes: 16));
        var stream = new NonSeekableStream(JsonBytes("TableB"));

        Func<Task> action = () => processor.SaveAsync(stream, "TableB");

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*MaxJsonPayloadBytes*");
        stream.BytesRead.Should().BeLessThan(JsonBytes("TableB").Length, "the payload is rejected before it is fully read");
        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task SaveAsync_ShouldWrite_WhenTheJsonPayloadIsExactlyAtTheLimit()
    {
        byte[] payload = JsonBytes("TableB");
        IBulkProcessor processor = CreateProcessor(new JsonPayloadPolicy(RequireDataMapping: false, MaxPayloadBytes: payload.Length));

        await processor.SaveAsync(new NonSeekableStream(payload), "TableB");

        _provider.Targets.Should().ContainSingle();
    }

    [Test]
    public async Task SaveAsync_ShouldThrowJsonException_WhenTheJsonNestsDeeperThan64Levels()
    {
        string deep = $$"""
            { "tableName": "TableB", "columns": [ { "name": "BaseInt", "type": "int" } ],
              "data": [ { "BaseInt": {{new string('[', 70)}}{{new string(']', 70)}} } ] }
            """;

        Func<Task> action = () => CreateProcessor().SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(deep)), "TableB");

        await action.Should().ThrowAsync<JsonException>();
        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
    }

    private BulkProcessor CreateProcessor(JsonPayloadPolicy policy = null) =>
        new(_provider.Object, ConnectionString, TestMappingRegistry.Valid(), _loggerFactory, policy);

    private void GivenTheProviderTranslatesWith(bool useSqlServerTranslator)
    {
        var translate = _provider.Mock.Setup(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()));
        if (useSqlServerTranslator)
        {
            translate.Returns<Exception, BulkWriteTarget>(SqlServerExceptionTranslator.Translate);
            return;
        }

        translate.Returns<Exception, BulkWriteTarget>((exception, _) => new InvalidOperationException(exception.Message));
    }

    private static bool LogEntryContains(RecordingLoggerFactory.LogEntry entry, string text) =>
        entry.Message.Contains(text, StringComparison.Ordinal) ||
        (entry.Exception != null && entry.Exception.ToString().Contains(text, StringComparison.Ordinal));

    private static FieldInfo[] FieldsHolding(object instance, string value) =>
        instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => Equals(field.GetValue(instance), value)).ToArray();

    private static Stream Json(string tableName) => new MemoryStream(JsonBytes(tableName));

    private static byte[] JsonBytes(string tableName) => Encoding.UTF8.GetBytes(
        $$"""
        {
          "tableName": "{{tableName}}",
          "columns": [ { "name": "BaseInt", "type": "int" }, { "name": "AnotherColumn", "type": "int" } ],
          "data": [ { "BaseInt": 1, "AnotherColumn": 2 } ]
        }
        """);
}

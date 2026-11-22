using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>
/// <see cref="DirectPathOracleWriter"/> without a database: the load plan (OracleBulkCopy needs an open connection, so its configuration
/// is checked through <see cref="DirectPathOracleWriter.Plan"/>), source-column and source-type checks, the Information entry and
/// cancellation before the (synchronous) load. The load itself is covered by <c>OracleDirectPathTests</c> in the Oracle integration tests.
/// </summary>
public class DirectPathOracleWriterTests
{
    private OracleConnection _connection;

    [SetUp]
    public void SetUp() => _connection = new OracleConnection();

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private DirectPathOracleWriter Writer(IOracleIdentifierResolver resolver = null, int batchSize = 0, int timeout = 30, ILogger logger = null) =>
        new(_connection, new BulkWriteOptions(batchSize, timeout), resolver ?? new UnquotedIdentifierResolver(), TypeMap, OracleProvider.Name,
            logger ?? NullLogger.Instance);

    private static readonly OracleTypeMap TypeMap = new(OracleBooleanBinding.Number);

    private static DataTable PostsTable()
    {
        var table = new DataTable("Posts");
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("BlogId", typeof(int));
        table.Rows.Add("First", 1);
        return table;
    }

    [Test]
    public void CopyOptions_ShouldBeDefault_WhenTheWriterIsCreated()
    {
        DirectPathOracleWriter.CopyOptions.Should().Be(OracleBulkCopyOptions.Default);
    }

    [Test]
    public void Plan_ShouldUseTheQuotedNamesBatchSizeAndTimeout_WhenTheTargetIsSchemaQualified()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(
            new BulkWriteTarget("loki.Posts", new[] { new ColumnMap("Title", "Title"), new ColumnMap("BlogId", "BlogId") }));

        DirectPathLoad load = DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(250, 45), TypeMap);

        load.SchemaSql.Should().Be("\"LOKI\"");
        load.TableSql.Should().Be("\"POSTS\"");
        load.BatchSize.Should().Be(250);
        load.TimeoutSeconds.Should().Be(45);
        load.Columns.Should().Equal(new DirectPathColumn(0, "\"TITLE\""), new DirectPathColumn(1, "\"BLOGID\""));
    }

    [Test]
    public void Plan_ShouldLeaveTheSchemaNull_WhenTheTableIsNotQualified()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }));

        DirectPathLoad load = DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap);

        load.SchemaSql.Should().BeNull();
        load.TableSql.Should().Be("\"POSTS\"");
        load.BatchSize.Should().Be(ArrayBindOracleWriter.DefaultChunkSize, "0 means 5000 rows per batch, as with array bind (bounded memory)");
    }

    [Test]
    public void Plan_ShouldMapEachColumnBySourceOrdinalInTargetOrder_WhenTheSourceHasThem()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(
            new BulkWriteTarget("Posts", new[] { new ColumnMap("BlogId", "BlogId"), new ColumnMap("Title", "Heading") }));

        DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap).Columns
            .Should().Equal(new DirectPathColumn(1, "\"BLOGID\""), new DirectPathColumn(0, "\"HEADING\""));
    }

    [Test]
    public void Plan_ShouldThrowAMarkedSourceColumnNotFound_WhenAMappedColumnIsMissing()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Missing", "Title") }));

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap);

        action.Should().Throw<InvalidOperationException>().WithMessage(BulkCopyErrorMessages.SourceColumnNotFound("Missing"))
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public void Plan_ShouldThrowAMarkedInvalidOperation_WhenThereAreNoColumns()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        var resolved = new OracleResolvedTarget(null, "\"POSTS\"", Array.Empty<OracleResolvedColumn>());

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap);

        action.Should().Throw<InvalidOperationException>().WithMessage(SourceColumns.NoColumnsMessage)
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public async Task WriteAsync_ShouldThrowOperationCanceledBeforeResolving_WhenTheTokenIsAlreadyCancelled()
    {
        var resolver = new RecordingResolver();
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Func<Task> action = () => Writer(resolver).WriteAsync(reader, new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }),
            new CancellationToken(true));

        await action.Should().ThrowAsync<OperationCanceledException>();
        resolver.Calls.Should().Be(0);
    }

    [Test]
    public async Task WriteAsync_ShouldThrowOperationCanceledBeforeTheLoad_WhenTheTokenIsCancelledWhileResolving()
    {
        using var cancellation = new CancellationTokenSource();
        var resolver = new RecordingResolver(onResolve: cancellation.Cancel);
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Func<Task> action = () => Writer(resolver).WriteAsync(reader, new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>("the token is checked again just before WriteToServer");
        resolver.Calls.Should().Be(1);
        _connection.State.Should().Be(ConnectionState.Closed);
    }

    [Test]
    public async Task WriteAsync_ShouldThrowArgumentNullException_WhenTheSourceOrTargetIsNull()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        await Writer().Invoking(w => w.WriteAsync(null, new BulkWriteTarget("Posts", Array.Empty<ColumnMap>()), CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
        await Writer().Invoking(w => w.WriteAsync(reader, null, CancellationToken.None)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAsync_ShouldLogTheSanitisedTableAndProviderAtInformation_WhenALoadStarts()
    {
        using var loggerFactory = new RecordingLoggerFactory();
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        DirectPathOracleWriter writer = Writer(logger: loggerFactory.CreateLogger("test"));

        Func<Task> action = () => writer.WriteAsync(reader, new BulkWriteTarget("Posts\r\nforged", new[] { new ColumnMap("Title", "Title") }),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>("the resolver rejects the name after the entry is logged");
        loggerFactory.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Information)
            .Which.Message.Should().Be("Direct-path load (staging tables only) to Posts??forged via Oracle");
    }

    [TestCase(0, 5000)]
    [TestCase(-1, 5000)]
    [TestCase(1, 1)]
    [TestCase(50_000, 50_000)]
    public void BatchSize_ShouldBeTheDefaultChunkSize_WhenTheBatchSizeIsZero(int batchSize, int expected)
    {
        DirectPathOracleWriter.BatchSize(batchSize).Should().Be(expected);
    }

    [Test]
    public void Plan_ShouldThrowAMarkedNotSupported_WhenASourceColumnTypeIsNotSupported()
    {
        using var table = new DataTable();
        table.Columns.Add("Payload", typeof(object));
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Payload", "Title") }));

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("The source column 'Payload' has type Object, which the Oracle provider does not support.")
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [TestCase(typeof(DateOnly))]
    [TestCase(typeof(TimeOnly?))]
    public void Plan_ShouldThrowAMarkedNotSupported_WhenTheTypeIsNotSupportedByDirectPath(Type type)
    {
        using var table = new DataTable();
        table.Columns.Add("When", Nullable.GetUnderlyingType(type) ?? type);
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("T", new[] { new ColumnMap("When", "When") }));

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30), TypeMap);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("The source column 'When' has type *, which Oracle direct-path loads do not support*")
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public void ThrowIfNotSupportedByDirectPath_ShouldAcceptTheNullableForm_WhenTheTypeIsSupported()
    {
        Action action = () => DirectPathOracleWriter.ThrowIfNotSupportedByDirectPath(typeof(DateTime?), "When");

        action.Should().NotThrow();
        FluentActions.Invoking(() => DirectPathOracleWriter.ThrowIfNotSupportedByDirectPath(typeof(TimeOnly?), "When"))
            .Should().Throw<NotSupportedException>();
    }

    /// <summary>Resolves like Unquoted and counts calls; <c>onResolve</c> runs during resolution.</summary>
    private sealed class RecordingResolver(Action onResolve = null) : IOracleIdentifierResolver
    {
        public int Calls { get; private set; }

        public Task<OracleResolvedTarget> ResolveAsync(OracleConnection connection, BulkWriteTarget target, int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            Calls++;
            onResolve?.Invoke();
            return Task.FromResult(UnquotedIdentifierResolver.Resolve(target));
        }
    }
}

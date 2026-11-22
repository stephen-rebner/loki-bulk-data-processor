using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>
/// <see cref="DirectPathOracleWriter"/> without a database: the load plan (OracleBulkCopy needs an open connection, so its configuration
/// is checked through <see cref="DirectPathOracleWriter.Plan"/>), source-column checks and cancellation before the (synchronous) load. The load itself is covered by <c>OracleDirectPathTests</c> in the Oracle integration tests.
/// </summary>
public class DirectPathOracleWriterTests
{
    private OracleConnection _connection;

    [SetUp]
    public void SetUp() => _connection = new OracleConnection();

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private DirectPathOracleWriter Writer(IOracleIdentifierResolver resolver = null, int batchSize = 0, int timeout = 30) =>
        new(_connection, new BulkWriteOptions(batchSize, timeout), resolver ?? new UnquotedIdentifierResolver(), NullLogger.Instance);

    private static DataTable PostsTable()
    {
        var table = new DataTable("Posts");
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("BlogId", typeof(int));
        table.Rows.Add("First", 1);
        return table;
    }

    [Test]
    public void CopyOptions_ShouldBeDefault_SoNoInternalTransactionOrIndexChecksAreUsed()
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

        DirectPathLoad load = DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(250, 45));

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

        DirectPathLoad load = DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30));

        load.SchemaSql.Should().BeNull();
        load.TableSql.Should().Be("\"POSTS\"");
        load.BatchSize.Should().Be(0, "0 sends every row in one batch, as SqlBulkCopy does");
    }

    [Test]
    public void Plan_ShouldMapEachColumnBySourceOrdinalInTargetOrder_WhenTheSourceHasThem()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(
            new BulkWriteTarget("Posts", new[] { new ColumnMap("BlogId", "BlogId"), new ColumnMap("Title", "Heading") }));

        DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30)).Columns
            .Should().Equal(new DirectPathColumn(1, "\"BLOGID\""), new DirectPathColumn(0, "\"HEADING\""));
    }

    [Test]
    public void Plan_ShouldThrowAMarkedSourceColumnNotFound_WhenAMappedColumnIsMissing()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Missing", "Title") }));

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30));

        action.Should().Throw<InvalidOperationException>().WithMessage(BulkCopyErrorMessages.SourceColumnNotFound("Missing"))
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public void Plan_ShouldThrowAMarkedInvalidOperation_WhenThereAreNoColumns()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        var resolved = new OracleResolvedTarget(null, "\"POSTS\"", Array.Empty<OracleResolvedColumn>());

        Action action = () => DirectPathOracleWriter.Plan(reader, resolved, new BulkWriteOptions(0, 30));

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
    public async Task DisposeAsync_ShouldNotDisposeTheConnection_WhenTheWriterIsDisposed()
    {
        bool disposed = false;
        _connection.Disposed += (_, _) => disposed = true;

        await Writer().DisposeAsync();

        disposed.Should().BeFalse();
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

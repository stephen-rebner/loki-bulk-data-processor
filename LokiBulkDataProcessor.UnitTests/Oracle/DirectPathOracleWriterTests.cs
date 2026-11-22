using System;
using System.Collections.Generic;
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
/// <see cref="DirectPathOracleWriter"/> without a database: OracleBulkCopy configuration, source-column checks and cancellation
/// before the (synchronous) load. The load itself is covered by <c>OracleDirectPathTests</c> in the Oracle integration tests.
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
    public void CreateBulkCopy_ShouldUseTheQuotedNamesBatchSizeAndTimeout_WhenTheTargetIsSchemaQualified()
    {
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(
            new BulkWriteTarget("loki.Posts", new[] { new ColumnMap("Title", "Title"), new ColumnMap("BlogId", "BlogId") }));

        using OracleBulkCopy bulkCopy = Writer(batchSize: 250, timeout: 45).CreateBulkCopy(resolved, new[] { 0, 1 });

        bulkCopy.DestinationSchemaName.Should().Be("\"LOKI\"");
        bulkCopy.DestinationTableName.Should().Be("\"POSTS\"");
        bulkCopy.BatchSize.Should().Be(250);
        bulkCopy.BulkCopyTimeout.Should().Be(45);
        bulkCopy.BulkCopyOptions.Should().Be(OracleBulkCopyOptions.Default);
        bulkCopy.ColumnMappings.Count.Should().Be(2);
        bulkCopy.ColumnMappings[0].SourceOrdinal.Should().Be(0);
        bulkCopy.ColumnMappings[0].DestinationColumnName.Should().Be("\"TITLE\"");
        bulkCopy.ColumnMappings[1].SourceOrdinal.Should().Be(1);
        bulkCopy.ColumnMappings[1].DestinationColumnName.Should().Be("\"BLOGID\"");
    }

    [Test]
    public void CreateBulkCopy_ShouldLeaveTheSchemaUnset_WhenTheTableIsNotQualified()
    {
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }));

        using OracleBulkCopy bulkCopy = Writer().CreateBulkCopy(resolved, new[] { 0 });

        bulkCopy.DestinationSchemaName.Should().BeNullOrEmpty();
        bulkCopy.DestinationTableName.Should().Be("\"POSTS\"");
    }

    [Test]
    public void SourceOrdinals_ShouldReturnTheOrdinalOfEachMappedColumnInTargetOrder_WhenTheSourceHasThem()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(
            new BulkWriteTarget("Posts", new[] { new ColumnMap("BlogId", "BlogId"), new ColumnMap("Title", "Heading") }));

        DirectPathOracleWriter.SourceOrdinals(reader, resolved).Should().Equal(1, 0);
    }

    [Test]
    public void SourceOrdinals_ShouldThrowAMarkedSourceColumnNotFound_WhenAMappedColumnIsMissing()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Missing", "Title") }));

        Action action = () => DirectPathOracleWriter.SourceOrdinals(reader, resolved);

        action.Should().Throw<InvalidOperationException>().WithMessage(BulkCopyErrorMessages.SourceColumnNotFound("Missing"))
            .Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public void SourceOrdinals_ShouldThrowAMarkedInvalidOperation_WhenThereAreNoColumns()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Action action = () => DirectPathOracleWriter.SourceOrdinals(reader, new OracleResolvedTarget(null, "\"POSTS\"", Array.Empty<OracleResolvedColumn>()));

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

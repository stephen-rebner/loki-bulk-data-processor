using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public partial class ArrayBindOracleWriterTests
{
    private OracleConnection _connection;

    [SetUp]
    public void SetUp() => _connection = new OracleConnection();

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private ArrayBindOracleWriter Writer(int batchSize = 0, int timeout = 30, OracleBooleanBinding booleans = OracleBooleanBinding.Number) =>
        new(_connection, null, new BulkWriteOptions(batchSize, timeout), new UnquotedIdentifierResolver(),
            new OracleTypeMap(booleans), NullLogger.Instance);

    private static DataTable PostsTable()
    {
        var table = new DataTable("Posts");
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("BlogId", typeof(int));
        table.Columns.Add("Published", typeof(bool));
        table.Rows.Add("First", 1, true);
        return table;
    }

    private static OracleResolvedTarget Resolve(params ColumnMap[] columns) =>
        UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", columns));

    [Test]
    public void BindColumns_ShouldUseTheSourceOrdinalAndTypeOfEachMappedColumn_WhenTheSourceHasThem()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        OracleBoundColumn[] columns = Writer().BindColumns(reader, Resolve(new ColumnMap("Published", "IsPublished"), new ColumnMap("Title", "Title")));

        columns.Should().HaveCount(2);
        columns[0].SourceOrdinal.Should().Be(2);
        columns[0].Binding.DbType.Should().Be(OracleDbType.Int16);
        columns[1].SourceOrdinal.Should().Be(0);
        columns[1].Binding.DbType.Should().Be(OracleDbType.Varchar2);
    }

    [Test]
    public void BindColumns_ShouldThrowAMarkedSourceColumnNotFound_WhenAMappedSourceColumnIsMissing()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Action action = () => Writer().BindColumns(reader, Resolve(new ColumnMap("Missing", "Title")));

        var thrown = action.Should().Throw<InvalidOperationException>().WithMessage(BulkCopyErrorMessages.SourceColumnNotFound("Missing"));
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [Test]
    public void BindColumns_ShouldThrow_WhenThereAreNoColumnsToCopy()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Action action = () => Writer().BindColumns(reader, Resolve());

        action.Should().Throw<InvalidOperationException>().WithMessage("There are no columns to copy*");
    }

    [Test]
    public void CreateInsertCommand_ShouldPrepareAPositionalArrayBoundInsert_WhenGivenAResolvedTarget()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();
        ArrayBindOracleWriter writer = Writer(timeout: 45);
        OracleResolvedTarget resolved = Resolve(new ColumnMap("Title", "Title"), new ColumnMap("BlogId", "BlogId"));

        using OracleCommand command = writer.CreateInsertCommand(resolved, writer.BindColumns(reader, resolved));

        command.CommandText.Should().Be("INSERT INTO \"POSTS\" (\"TITLE\", \"BLOGID\") VALUES (:1, :2)");
        command.BindByName.Should().BeFalse();
        command.CommandTimeout.Should().Be(45);
        command.Connection.Should().BeSameAs(_connection);
        command.Parameters.Count.Should().Be(2);
        command.Parameters[0].OracleDbType.Should().Be(OracleDbType.Varchar2);
        command.Parameters[1].OracleDbType.Should().Be(OracleDbType.Int32);
        command.Parameters[1].Direction.Should().Be(ParameterDirection.Input);
    }

    [Test]
    public void BindChunk_ShouldSetArrayBindCountAndOneExactLengthArrayPerParameter_WhenTheChunkIsPartial()
    {
        using DataTable table = PostsTable();
        table.Rows.Add("Second", 2, false);
        using DataTableReader reader = table.CreateDataReader();
        ArrayBindOracleWriter writer = Writer();
        OracleResolvedTarget resolved = Resolve(new ColumnMap("Title", "Title"), new ColumnMap("Published", "Published"));
        OracleBoundColumn[] columns = writer.BindColumns(reader, resolved);
        using OracleCommand command = writer.CreateInsertCommand(resolved, columns);
        var buffer = new ColumnBuffer(columns, 10);
        while (reader.Read())
        {
            buffer.AddRow(reader);
        }

        ArrayBindOracleWriter.BindChunk(command, buffer);

        command.ArrayBindCount.Should().Be(2);
        ((object[])command.Parameters[0].Value).Should().Equal("First", "Second");
        ((object[])command.Parameters[1].Value).Should().Equal((short)1, (short)0);
    }

    [Test]
    public async Task WriteAsync_ShouldThrowBeforeTouchingTheConnection_WhenTheTokenIsAlreadyCancelled()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Func<Task> action = () => Writer().WriteAsync(reader, new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }),
            new CancellationToken(canceled: true));

        await action.Should().ThrowAsync<OperationCanceledException>();
        _connection.State.Should().Be(ConnectionState.Closed);
    }

    [Test]
    public async Task WriteAsync_ShouldRejectAHostileTableNameBeforeAnySql_WhenUsingUnquotedResolution()
    {
        using DataTable table = PostsTable();
        using DataTableReader reader = table.CreateDataReader();

        Func<Task> action = () => Writer().WriteAsync(reader,
            new BulkWriteTarget("TestDbModels; DROP TABLE Blogs--", new[] { new ColumnMap("Title", "Title") }), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("destinationTableName");
        _connection.State.Should().Be(ConnectionState.Closed, "no command was executed on the connection");
    }

    [Test]
    public async Task DisposeAsync_ShouldNotDisposeTheConnection_WhenTheWriterIsDisposed()
    {
        bool connectionDisposed = false;
        _connection.Disposed += (_, _) => connectionDisposed = true;
        ArrayBindOracleWriter writer = Writer();

        await writer.DisposeAsync();

        connectionDisposed.Should().BeFalse("the connection belongs to the pipeline (or the caller), never to the writer");
    }
}

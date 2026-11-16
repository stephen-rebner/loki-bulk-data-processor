using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>Chunking of <see cref="ArrayBindOracleWriter"/>: chunk size, flushing and cancellation, without a database.</summary>
public partial class ArrayBindOracleWriterTests
{
    [Test]
    public async Task CopyInChunksAsync_ShouldExecuteChunksOfTheBatchSize_WhenTheRowsDoNotDivideEvenly()
    {
        using DataTable table = ColumnBufferTests.Rows(10);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(ColumnBufferTests.Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), ArrayBindOracleWriter.ChunkSize(3));
        var chunks = new List<object[]>();

        (int rows, int executions) = await ArrayBindOracleWriter.CopyInChunksAsync(reader, buffer,
            (chunk, _) => { chunks.Add(chunk.ValuesFor(0).ToArray()); return Task.CompletedTask; }, CancellationToken.None);

        rows.Should().Be(10);
        executions.Should().Be(4);
        chunks.Select(c => c.Length).Should().Equal(3, 3, 3, 1);
        chunks.SelectMany(c => c).Should().Equal(Enumerable.Range(1, 10).Cast<object>());
        buffer.Count.Should().Be(0);
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldNotExecute_WhenTheSourceHasNoRows()
    {
        using DataTable table = ColumnBufferTests.Rows(0);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(ColumnBufferTests.Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 5);
        int executions = 0;

        var result = await ArrayBindOracleWriter.CopyInChunksAsync(reader, buffer, (_, _) => { executions++; return Task.CompletedTask; }, CancellationToken.None);

        result.Should().Be((0, 0));
        executions.Should().Be(0);
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldPropagateTheError_WhenAChunkFails()
    {
        using DataTable table = ColumnBufferTests.Rows(5);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(ColumnBufferTests.Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 2);

        Func<Task> action = () => ArrayBindOracleWriter.CopyInChunksAsync(reader, buffer,
            (_, _) => throw new InvalidOperationException("server rejected chunk"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("server rejected chunk");
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldFlushEarly_WhenThePayloadBudgetIsReached()
    {
        DataTable table = ColumnBufferTests.Rows(5);
        var buffer = new ColumnBuffer(ColumnBufferTests.Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 100, maxPayloadBytes: 16);
        var chunkSizes = new List<int>();

        (int rows, int executions) = await ArrayBindOracleWriter.CopyInChunksAsync(table.CreateDataReader(), buffer,
            (chunk, _) => { chunkSizes.Add(chunk.Count); return Task.CompletedTask; }, CancellationToken.None);

        chunkSizes.Should().Equal(2, 2, 1);
        (rows, executions).Should().Be((5, 3));
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldStopBeforeTheNextChunk_WhenTheTokenIsCancelledBetweenChunks()
    {
        DataTable table = ColumnBufferTests.Rows(6);
        var buffer = new ColumnBuffer(ColumnBufferTests.Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 2);
        using var cancellation = new CancellationTokenSource();
        int executions = 0;

        Func<Task> action = () => ArrayBindOracleWriter.CopyInChunksAsync(table.CreateDataReader(), buffer,
            (_, _) => { executions++; cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        executions.Should().Be(1);
    }

    [TestCase(1, 1)]
    [TestCase(5000, 5000)]
    [TestCase(ArrayBindOracleWriter.MaxChunkSize, ArrayBindOracleWriter.MaxChunkSize)]
    public void ChunkSize_ShouldFollowTheBatchSize_WhenItIsWithinBounds(int batchSize, int expected)
    {
        ArrayBindOracleWriter.ChunkSize(batchSize).Should().Be(expected);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ChunkSize_ShouldUseTheDefault_WhenTheBatchSizeIsZeroOrLess(int batchSize)
    {
        ArrayBindOracleWriter.ChunkSize(batchSize).Should().Be(ArrayBindOracleWriter.DefaultChunkSize);
    }

    [TestCase(ArrayBindOracleWriter.MaxChunkSize + 1)]
    [TestCase(int.MaxValue)]
    public void ChunkSize_ShouldBeCapped_WhenTheBatchSizeIsAboveTheMaximum(int batchSize)
    {
        ArrayBindOracleWriter.ChunkSize(batchSize).Should().Be(ArrayBindOracleWriter.MaxChunkSize);
    }

    [TestCase(5000, 2, 5000)]
    [TestCase(20_000, 100, 10_000)]
    [TestCase(100_000, 1000, 1000)]
    [TestCase(0, 1000, 1000)]
    [TestCase(10, 2_000_000, 1)]
    [TestCase(10, 0, 10)]
    public void ChunkSize_ShouldStayWithinTheCellBudget_WhenTheTableIsWide(int batchSize, int columnCount, int expected)
    {
        ArrayBindOracleWriter.ChunkSize(batchSize, columnCount).Should().Be(expected);
    }
}

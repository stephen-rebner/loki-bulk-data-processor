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

public class ColumnBufferTests
{
    private static OracleBoundColumn[] Columns(OracleTypeMap map, DataTable table) =>
        table.Columns.Cast<DataColumn>().Select(c => new OracleBoundColumn(c.Ordinal, map.For(c.DataType, c.ColumnName))).ToArray();

    private static DataTable Rows(int count)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        for (int i = 1; i <= count; i++)
        {
            table.Rows.Add(i, $"Row{i}");
        }

        return table;
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldExecuteChunksOfTheBatchSize_WhenTheRowsDoNotDivideEvenly()
    {
        using DataTable table = Rows(10);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), ArrayBindOracleWriter.ChunkSize(3));
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
        using DataTable table = Rows(0);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 5);
        int executions = 0;

        var result = await ArrayBindOracleWriter.CopyInChunksAsync(reader, buffer, (_, _) => { executions++; return Task.CompletedTask; }, CancellationToken.None);

        result.Should().Be((0, 0));
        executions.Should().Be(0);
    }

    [Test]
    public async Task CopyInChunksAsync_ShouldPropagateTheError_WhenAChunkFails()
    {
        using DataTable table = Rows(5);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 2);

        Func<Task> action = () => ArrayBindOracleWriter.CopyInChunksAsync(reader, buffer,
            (_, _) => throw new InvalidOperationException("server rejected chunk"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("server rejected chunk");
    }

    [TestCase(0, ArrayBindOracleWriter.DefaultChunkSize)]
    [TestCase(1, 1)]
    [TestCase(5000, 5000)]
    [TestCase(100_000, 100_000)]
    [TestCase(int.MaxValue, ArrayBindOracleWriter.MaxChunkSize)]
    public void ChunkSize_ShouldFollowTheBatchSize_WhenItIsWithinBounds(int batchSize, int expected)
    {
        ArrayBindOracleWriter.ChunkSize(batchSize).Should().Be(expected);
        ArrayBindOracleWriter.DefaultChunkSize.Should().Be(5000);
    }

    [Test]
    public void ValuesFor_ShouldReturnExactLengthArrays_WhenTheBufferIsPartlyFilled()
    {
        using DataTable table = Rows(2);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 5);

        while (reader.Read())
        {
            buffer.AddRow(reader);
        }

        buffer.Count.Should().Be(2);
        buffer.IsFull.Should().BeFalse();
        buffer.ValuesFor(0).Should().Equal(1, 2);
        buffer.ValuesFor(1).Should().Equal("Row1", "Row2");
    }

    [Test]
    public void AddRow_ShouldStoreDbNull_WhenTheSourceValueIsNullOrDbNull()
    {
        var table = new DataTable();
        table.Columns.Add("Flag", typeof(bool));
        table.Columns.Add("When", typeof(DateTime));
        table.Rows.Add(DBNull.Value, DBNull.Value);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 1);
        reader.Read();

        buffer.AddRow(reader);

        buffer.IsFull.Should().BeTrue();
        buffer.ValuesFor(0).Should().Equal(DBNull.Value);
        buffer.ValuesFor(1).Should().Equal(DBNull.Value);
    }

    [TestCase(OracleBooleanBinding.Number, (short)1, (short)0)]
    [TestCase(OracleBooleanBinding.Boolean, true, false)]
    public void AddRow_ShouldConvertBooleansPerBinding_WhenTheColumnIsBool(OracleBooleanBinding binding, object expectedTrue, object expectedFalse)
    {
        var table = new DataTable();
        table.Columns.Add("Flag", typeof(bool));
        table.Rows.Add(true);
        table.Rows.Add(false);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(binding), table), 2);

        while (reader.Read())
        {
            buffer.AddRow(reader);
        }

        buffer.ValuesFor(0).Should().Equal(expectedTrue, expectedFalse);
    }

    [Test]
    public void Clear_ShouldReleaseReferencesAndResetTheCount_WhenCalledAfterAChunk()
    {
        using DataTable table = Rows(2);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 2);
        while (reader.Read())
        {
            buffer.AddRow(reader);
        }

        object[] fullArray = buffer.ValuesFor(1);
        buffer.Clear();

        buffer.Count.Should().Be(0);
        fullArray.Should().OnlyContain(value => value == null, "the reused array no longer holds the previous chunk's values");
    }

    [Test]
    public void AddRow_ShouldThrow_WhenTheBufferIsFull()
    {
        using DataTable table = Rows(2);
        using DataTableReader reader = table.CreateDataReader();
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 1);
        reader.Read();
        buffer.AddRow(reader);
        reader.Read();

        Action action = () => buffer.AddRow(reader);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Constructor_ShouldThrow_WhenTheCapacityIsLessThanOne()
    {
        Action action = () => new ColumnBuffer(Array.Empty<OracleBoundColumn>(), 0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}

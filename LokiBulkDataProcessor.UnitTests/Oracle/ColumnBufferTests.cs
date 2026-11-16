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
    internal static OracleBoundColumn[] Columns(OracleTypeMap map, DataTable table) =>
        table.Columns.Cast<DataColumn>().Select(c => new OracleBoundColumn(c.Ordinal, map.For(c.DataType, c.ColumnName))).ToArray();

    internal static DataTable Rows(int count)
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

    [Test]
    public void IsFull_ShouldBeTrue_WhenTheStringPayloadReachesTheByteBudgetBeforeTheRowCapacity()
    {
        DataTable table = Rows(3);   // "RowN" = 4 chars = 8 bytes per row
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 100, maxPayloadBytes: 16);
        using DataTableReader reader = table.CreateDataReader();

        reader.Read();
        buffer.AddRow(reader);
        buffer.IsFull.Should().BeFalse();
        reader.Read();
        buffer.AddRow(reader);

        buffer.IsFull.Should().BeTrue();
        buffer.Count.Should().Be(2);
        buffer.PayloadBytes.Should().Be(16);
        buffer.ValuesFor(1).Should().Equal("Row1", "Row2");
    }

    [Test]
    public void Clear_ShouldResetThePayloadBytes_WhenTheChunkIsFlushed()
    {
        DataTable table = Rows(1);
        var buffer = new ColumnBuffer(Columns(new OracleTypeMap(OracleBooleanBinding.Number), table), 5, maxPayloadBytes: 8);
        using DataTableReader reader = table.CreateDataReader();
        reader.Read();
        buffer.AddRow(reader);

        buffer.Clear();

        buffer.PayloadBytes.Should().Be(0);
        buffer.IsFull.Should().BeFalse();
    }
}

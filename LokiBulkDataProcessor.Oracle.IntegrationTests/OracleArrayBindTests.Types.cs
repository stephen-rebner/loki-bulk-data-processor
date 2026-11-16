using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// How each .NET type is bound and stored (OracleTypeMap), including <see cref="DBNull"/> entries inside the bound arrays and VARCHAR2
/// arrays of mixed lengths without an ArrayBindSize.
/// </summary>
public sealed partial class OracleArrayBindTests
{
    private const string OracleTypesTable = "OracleTypes";

    private const string SelectText = "SELECT Label, TextColumn FROM OracleTypes";

    private const string SelectTextLength = "SELECT Label, LENGTH(TextColumn) FROM OracleTypes";

    private const string SelectClobLength = "SELECT Label, DBMS_LOB.GETLENGTH(ClobColumn) FROM OracleTypes";

    private const string SelectNumberFlag = "SELECT Label, NumberFlag FROM OracleTypes";

    private const string SelectBooleanFlag = "SELECT Label, BooleanFlag FROM OracleTypes";

    private const string SelectGuid = "SELECT GuidColumn, RAWTOHEX(GuidColumn) FROM OracleTypes";

    private const string SelectValues = """
        SELECT Label, TimestampColumn, OffsetColumn, IntervalColumn, DateColumn, DecimalColumn, DoubleColumn, LongColumn, BytesColumn
        FROM OracleTypes
        """;

    private static readonly DateTime Timestamp = new DateTime(2026, 11, 16, 12, 34, 56).AddTicks(1234567);

    private static readonly DateTimeOffset Offset = new(2026, 11, 16, 12, 34, 56, TimeSpan.FromHours(5.5));

    private static readonly TimeSpan Interval = new TimeSpan(3, 4, 5, 6).Add(TimeSpan.FromTicks(7654321));

    [Test]
    public async Task SaveAsync_ShouldStoreNull_WhenAStringIsEmpty()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("TextColumn", typeof(string)));
        rows.Rows.Add("empty", "");
        rows.Rows.Add("value", "x");

        await Processor().SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectText, reader => (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1))))
            .Should().BeEquivalentTo(new[] { ("empty", (string)null), ("value", "x") }, "Oracle stores an empty string as NULL");
    }

    [Test]
    public async Task SaveAsync_ShouldStoreEveryString_WhenOneChunkMixesLengthsWithoutAnArrayBindSize()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("TextColumn", typeof(string)));
        rows.Rows.Add("short", "a");
        rows.Rows.Add("max", new string('x', 4000));
        rows.Rows.Add("null", DBNull.Value);
        rows.Rows.Add("multibyte", new string('é', 2000));

        await Processor().SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectTextLength, reader => (reader.GetString(0), reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1)))))
            .Should().BeEquivalentTo(new[] { ("short", (int?)1), ("max", 4000), ("null", null), ("multibyte", 2000) });
    }

    /// <summary>
    /// Step 7 finding: Oracle 23.26 with ODP.NET 23.26.400 stores strings longer than the VARCHAR2 bind limit in a CLOB column
    /// (step 6 had documented ORA-01461 for them; that applies to older servers only).
    /// </summary>
    [Test]
    public async Task SaveAsync_ShouldStoreStringsLongerThanTheVarchar2BindLimit_WhenTheColumnIsAClob()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("ClobColumn", typeof(string)));
        rows.Rows.Add("short", "clob text");
        rows.Rows.Add("long", new string('c', 100_000));

        await Processor().SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectClobLength, reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1)))))
            .Should().BeEquivalentTo(new[] { ("short", 9), ("long", 100_000) });
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheOra12899MessageWithoutTheValue_WhenAStringIsLongerThanItsVarchar2Column()
    {
        string tooLong = new('v', 4001);
        using DataTable rows = Table(("Label", typeof(string)), ("TextColumn", typeof(string)));
        rows.Rows.Add("fits", "x");
        rows.Rows.Add("too long", tooLong);

        Func<Task> action = () => Processor().SaveAsync(rows, OracleTypesTable);

        (await ThrowsTranslated(action, "Bulk copy to 'OracleTypes' failed (Oracle error ORA-12899). See InnerException for details.", 12899))
            .Which.Message.Should().NotContain(tooLong);
        (await _database.QueryAsync(SelectText, reader => reader.GetString(0))).Should().BeEmpty("Loki's own transaction is rolled back");
    }

    [Test]
    public async Task SaveAsync_ShouldStoreOneZeroAndNull_WhenBooleansAreBoundAsNumbers()
    {
        using DataTable rows = FlagRows("NumberFlag");

        await Processor().SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectNumberFlag, reader => (reader.GetString(0), reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1)))))
            .Should().BeEquivalentTo(new[] { ("true", (int?)1), ("false", 0), ("null", null) });
    }

    [TestCase(OracleProvider.Name)]
    [TestCase(BooleanProvider)]
    public async Task SaveAsync_ShouldStoreTrueFalseAndNull_WhenTheColumnIsABoolean(string providerName)
    {
        using DataTable rows = FlagRows("BooleanFlag");

        await Processor(providerName).SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectBooleanFlag, reader => (reader.GetString(0), reader.IsDBNull(1) ? (bool?)null : reader.GetBoolean(1))))
            .Should().BeEquivalentTo(new[] { ("true", (bool?)true), ("false", false), ("null", null) });
    }

    [Test]
    public async Task SaveAsync_ShouldStoreOneZeroAndNull_WhenBooleansAreBoundAsBooleanIntoANumberColumn()
    {
        using DataTable rows = FlagRows("NumberFlag");

        await Processor(BooleanProvider).SaveAsync(rows, OracleTypesTable);

        (await _database.QueryAsync(SelectNumberFlag, reader => (reader.GetString(0), reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1)))))
            .Should().BeEquivalentTo(new[] { ("true", (int?)1), ("false", 0), ("null", null) });
    }

    [Test]
    public async Task SaveAsync_ShouldStoreTheDotNetByteOrder_WhenAGuidIsWrittenToRaw16()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        using DataTable rows = Table(("Label", typeof(string)), ("GuidColumn", typeof(Guid)));
        rows.Rows.Add("guid", guid);

        await Processor().SaveAsync(rows, OracleTypesTable);

        (byte[] Bytes, string Hex) stored = (await _database.QueryAsync(SelectGuid, reader => ((byte[])reader.GetValue(0), reader.GetString(1)))).Single();
        stored.Bytes.Should().Equal(guid.ToByteArray());
        stored.Hex.Should().Be("33221100554477668899AABBCCDDEEFF", "Guid.ToByteArray is little-endian in the first three groups");
    }

    [Test]
    public async Task SaveAsync_ShouldRoundTripEveryValueAndEveryNull_WhenOneChunkMixesValuesAndNulls()
    {
        using DataTable rows = ValueRows();

        await Processor().SaveAsync(rows, OracleTypesTable);

        DbDataReaderRow[] stored = (await _database.QueryAsync(SelectValues, DbDataReaderRow.Read)).ToArray();
        stored.Single(row => row.Label == "values").Should().BeEquivalentTo(new DbDataReaderRow("values", Timestamp, Offset, Interval,
            new DateTime(2026, 11, 16), 123456789012345678.0123456789m, Math.PI, long.MaxValue, new byte[] { 1, 2, 3 }));
        stored.Single(row => row.Label == "nulls").Should().BeEquivalentTo(new DbDataReaderRow("nulls", null, null, null, null, null, null, null, null));
        stored.Single(row => row.Label == "time only").Interval.Should().Be(new TimeSpan(0, 13, 14, 15));
    }

    private static DataTable FlagRows(string column)
    {
        DataTable rows = Table(("Label", typeof(string)), (column, typeof(bool)));
        rows.Rows.Add("true", true);
        rows.Rows.Add("false", false);
        rows.Rows.Add("null", DBNull.Value);
        return rows;
    }

    /// <summary>A row with a value in every typed column, a row of nulls, and a TimeOnly row (all in one chunk).</summary>
    private static DataTable ValueRows()
    {
        DataTable rows = Table(("Label", typeof(string)), ("TimestampColumn", typeof(DateTime)), ("OffsetColumn", typeof(DateTimeOffset)),
            ("IntervalColumn", typeof(TimeSpan)), ("DateColumn", typeof(DateOnly)), ("DecimalColumn", typeof(decimal)),
            ("DoubleColumn", typeof(double)), ("LongColumn", typeof(long)), ("BytesColumn", typeof(byte[])));
        rows.Rows.Add("values", Timestamp, Offset, Interval, new DateOnly(2026, 11, 16), 123456789012345678.0123456789m, Math.PI,
            long.MaxValue, new byte[] { 1, 2, 3 });
        rows.Rows.Add("nulls", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
        rows.Rows.Add("time only", DBNull.Value, DBNull.Value, new TimeOnly(13, 14, 15).ToTimeSpan(), DBNull.Value, DBNull.Value,
            DBNull.Value, DBNull.Value, DBNull.Value);
        return rows;
    }

    /// <summary>One OracleTypes row as read back through ODP.NET's typed getters.</summary>
    private sealed record DbDataReaderRow(string Label, DateTime? Timestamp, DateTimeOffset? Offset, TimeSpan? Interval, DateTime? Date,
        decimal? Decimal, double? Double, long? Long, byte[] Bytes)
    {
        public static DbDataReaderRow Read(DbDataReader reader)
        {
            var oracle = (OracleDataReader)reader;
            return new DbDataReaderRow(oracle.GetString(0), Nullable(oracle, 1, oracle.GetDateTime), Nullable(oracle, 2, oracle.GetDateTimeOffset),
                Nullable(oracle, 3, oracle.GetTimeSpan), Nullable(oracle, 4, oracle.GetDateTime), Nullable(oracle, 5, oracle.GetDecimal),
                Nullable(oracle, 6, oracle.GetDouble), Nullable(oracle, 7, oracle.GetInt64), oracle.IsDBNull(8) ? null : (byte[])oracle.GetValue(8));
        }

        private static T? Nullable<T>(OracleDataReader reader, int ordinal, Func<int, T> read) where T : struct =>
            reader.IsDBNull(ordinal) ? null : read(ordinal);
    }
}

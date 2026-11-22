using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Type parity (code review W2): every .NET type that <c>OracleTypeMap</c> accepts for array bind, loaded through the DirectPath
/// registration into the matching OracleTypes column and read back. Loki only validates the source type for direct path; ODP.NET
/// converts the values itself. Bool is covered in <c>.Saves</c>; an unsupported type (<c>object</c>) fails before the load, and so do
/// <c>DateOnly</c> and <c>TimeOnly</c>, which array bind converts but ODP.NET's direct path cannot (InvalidCastException / ArgumentException).
/// </summary>
public sealed partial class OracleDirectPathTests
{
    private static readonly IReadOnlyDictionary<string, string> SelectTypedColumn = new Dictionary<string, string>
    {
        ["TextColumn"] = "SELECT TextColumn FROM OracleTypes",
        ["ClobColumn"] = "SELECT ClobColumn FROM OracleTypes",
        ["GuidColumn"] = "SELECT GuidColumn FROM OracleTypes",
        ["BytesColumn"] = "SELECT BytesColumn FROM OracleTypes",
        ["TimestampColumn"] = "SELECT TimestampColumn FROM OracleTypes",
        ["OffsetColumn"] = "SELECT OffsetColumn FROM OracleTypes",
        ["IntervalColumn"] = "SELECT IntervalColumn FROM OracleTypes",
        ["DateColumn"] = "SELECT DateColumn FROM OracleTypes",
        ["DecimalColumn"] = "SELECT DecimalColumn FROM OracleTypes",
        ["DoubleColumn"] = "SELECT DoubleColumn FROM OracleTypes",
        ["LongColumn"] = "SELECT LongColumn FROM OracleTypes",
    };

    private static readonly DateTime TypesTimestamp = new DateTime(2026, 11, 16, 12, 34, 56).AddTicks(1234567);

    private static IEnumerable<TestCaseData> DirectPathTypeCases()
    {
        yield return TypeCase(typeof(string), "TextColumn", "text", r => r.GetString(0));
        yield return TypeCase(typeof(char), "TextColumn", 'c', r => r.GetString(0), "c");
        yield return TypeCase(typeof(string), "ClobColumn", new string('c', 100_000), r => r.GetString(0).Length, 100_000, "long CLOB");
        yield return TypeCase(typeof(byte), "LongColumn", (byte)200, r => r.GetInt64(0), 200L);
        yield return TypeCase(typeof(sbyte), "LongColumn", (sbyte)-5, r => r.GetInt64(0), -5L);
        yield return TypeCase(typeof(short), "LongColumn", short.MinValue, r => r.GetInt64(0), (long)short.MinValue);
        yield return TypeCase(typeof(ushort), "LongColumn", ushort.MaxValue, r => r.GetInt64(0), (long)ushort.MaxValue);
        yield return TypeCase(typeof(int), "LongColumn", int.MinValue, r => r.GetInt64(0), (long)int.MinValue);
        yield return TypeCase(typeof(uint), "LongColumn", uint.MaxValue, r => r.GetInt64(0), (long)uint.MaxValue);
        yield return TypeCase(typeof(long), "LongColumn", long.MaxValue, r => r.GetInt64(0));
        yield return TypeCase(typeof(ulong), "LongColumn", (ulong)long.MaxValue + 1, r => r.GetDecimal(0), (decimal)long.MaxValue + 1);
        yield return TypeCase(typeof(DayOfWeek), "LongColumn", DayOfWeek.Friday, r => r.GetInt64(0), 5L, "enum");
        yield return TypeCase(typeof(decimal), "DecimalColumn", 123456789012345678.0123456789m, r => r.GetDecimal(0));
        yield return TypeCase(typeof(double), "DoubleColumn", Math.PI, r => r.GetDouble(0));
        yield return TypeCase(typeof(float), "DoubleColumn", 1.5f, r => r.GetDouble(0), 1.5d);
        yield return TypeCase(typeof(DateTime), "TimestampColumn", TypesTimestamp, r => r.GetDateTime(0));
        yield return TypeCase(typeof(DateTimeOffset), "OffsetColumn", new DateTimeOffset(TypesTimestamp, TimeSpan.FromHours(5.5)),
            r => r.GetDateTimeOffset(0));
        yield return TypeCase(typeof(TimeSpan), "IntervalColumn", new TimeSpan(3, 4, 5, 6).Add(TimeSpan.FromTicks(7654321)), r => r.GetTimeSpan(0));
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        yield return TypeCase(typeof(Guid), "GuidColumn", guid, r => (byte[])r.GetValue(0), guid.ToByteArray());
        yield return TypeCase(typeof(byte[]), "BytesColumn", new byte[] { 1, 2, 3 }, r => (byte[])r.GetValue(0));
    }

    [TestCaseSource(nameof(DirectPathTypeCases))]
    public async Task SaveAsync_ShouldStoreTheValue_WhenADirectPathLoadWritesEachSupportedType(Type type, string column, object value,
        Func<OracleDataReader, object> read, object expected)
    {
        using DataTable rows = Table(("Label", typeof(string)), (column, type));
        rows.Rows.Add("value", value);
        rows.Rows.Add("null", DBNull.Value);

        await Processor().SaveAsync(rows, "OracleTypes");

        IReadOnlyList<object> stored = await _database.QueryAsync(SelectTypedColumn[column],
            reader => reader.IsDBNull(0) ? null : read((OracleDataReader)reader));
        stored.Should().BeEquivalentTo(new[] { expected, null });
    }

    [Test]
    public async Task SaveAsync_ShouldThrowTheMarkedNotSupportedBeforeTheLoad_WhenASourceColumnIsAnObject()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("TextColumn", typeof(object)));
        rows.Rows.Add("value", "text");

        Func<Task> action = () => Processor().SaveAsync(rows, "OracleTypes");

        await action.Should().ThrowExactlyAsync<NotSupportedException>()
            .WithMessage("The source column 'TextColumn' has type Object, which the Oracle provider does not support.");
        (await _database.QueryAsync(SelectTypedColumn["TextColumn"], reader => reader.GetValue(0))).Should().BeEmpty();
    }

    [TestCase(typeof(DateOnly), "DateColumn")]
    [TestCase(typeof(TimeOnly), "IntervalColumn")]
    public async Task SaveAsync_ShouldThrowTheMarkedNotSupportedBeforeTheLoad_WhenTheTypeIsNotSupportedByDirectPath(Type type, string column)
    {
        using DataTable rows = Table(("Label", typeof(string)), (column, type));
        rows.Rows.Add("value", Activator.CreateInstance(type));

        Func<Task> action = () => Processor().SaveAsync(rows, "OracleTypes");

        await action.Should().ThrowExactlyAsync<NotSupportedException>()
            .WithMessage($"The source column '{column}' has type {type.Name}, which Oracle direct-path loads do not support*");
        (await _database.QueryAsync(SelectTypedColumn["TextColumn"], reader => reader.GetValue(0))).Should().BeEmpty();
    }

    private static TestCaseData TypeCase(Type type, string column, object value, Func<OracleDataReader, object> read, object expected = null,
        string name = null) =>
        new TestCaseData(type, column, value, read, expected ?? value).SetArgDisplayNames(name ?? type.Name, column);
}

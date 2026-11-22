using System;
using System.Collections.Generic;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleTypeMapTests
{
    private readonly OracleTypeMap _numberBooleans = new(OracleBooleanBinding.Number);

    private enum ByteEnum : byte { One = 1 }

    private enum LongEnum : long { Big = 5_000_000_000 }

    private enum UIntEnum : uint { Max = uint.MaxValue }

    private static IEnumerable<TestCaseData> AsIsTypes()
    {
        yield return new TestCaseData(typeof(string), OracleDbType.Varchar2, "text");
        yield return new TestCaseData(typeof(byte), OracleDbType.Byte, (byte)7);
        yield return new TestCaseData(typeof(short), OracleDbType.Int16, (short)7);
        yield return new TestCaseData(typeof(int), OracleDbType.Int32, 7);
        yield return new TestCaseData(typeof(long), OracleDbType.Int64, 7L);
        yield return new TestCaseData(typeof(decimal), OracleDbType.Decimal, 12345678901234567890.123456789m);
        yield return new TestCaseData(typeof(double), OracleDbType.BinaryDouble, 1.5d);
        yield return new TestCaseData(typeof(float), OracleDbType.BinaryFloat, 1.5f);
        yield return new TestCaseData(typeof(DateTime), OracleDbType.TimeStamp, new DateTime(2026, 11, 15, 10, 30, 0));
        yield return new TestCaseData(typeof(DateTimeOffset), OracleDbType.TimeStampTZ, new DateTimeOffset(2026, 11, 15, 10, 30, 0, TimeSpan.FromHours(2)));
        yield return new TestCaseData(typeof(TimeSpan), OracleDbType.IntervalDS, TimeSpan.FromMinutes(90));
        yield return new TestCaseData(typeof(byte[]), OracleDbType.Raw, new byte[] { 1, 2, 3 });
        yield return new TestCaseData(typeof(int?), OracleDbType.Int32, 7);
        yield return new TestCaseData(typeof(DateTime?), OracleDbType.TimeStamp, new DateTime(2026, 1, 1));
    }

    [TestCaseSource(nameof(AsIsTypes))]
    public void For_ShouldBindTheValueUnchanged_WhenTheTypeMapsDirectly(Type type, OracleDbType dbType, object value)
    {
        OracleBinding binding = _numberBooleans.For(type, "Column");

        binding.DbType.Should().Be(dbType);
        binding.Convert(value).Should().BeSameAs(value);
    }

    private static IEnumerable<TestCaseData> ConvertedTypes()
    {
        yield return new TestCaseData(typeof(char), OracleDbType.Varchar2, 'x', "x");
        yield return new TestCaseData(typeof(sbyte), OracleDbType.Int16, (sbyte)-5, (short)-5);
        yield return new TestCaseData(typeof(ushort), OracleDbType.Int32, ushort.MaxValue, (int)ushort.MaxValue);
        yield return new TestCaseData(typeof(uint), OracleDbType.Int64, uint.MaxValue, (long)uint.MaxValue);
        yield return new TestCaseData(typeof(ulong), OracleDbType.Decimal, ulong.MaxValue, (decimal)ulong.MaxValue);
        yield return new TestCaseData(typeof(ByteEnum), OracleDbType.Byte, ByteEnum.One, (byte)1);
        yield return new TestCaseData(typeof(LongEnum?), OracleDbType.Int64, LongEnum.Big, 5_000_000_000L);
        yield return new TestCaseData(typeof(UIntEnum), OracleDbType.Int64, UIntEnum.Max, (long)uint.MaxValue);
    }

    [TestCaseSource(nameof(ConvertedTypes))]
    public void For_ShouldConvertTheValue_WhenTheTypeNeedsConversion(Type type, OracleDbType dbType, object value, object expected)
    {
        OracleBinding binding = _numberBooleans.For(type, "Column");

        binding.DbType.Should().Be(dbType);
        binding.Convert(value).Should().Be(expected).And.BeOfType(expected.GetType());
    }

    [Test]
    public void For_ShouldStoreGuidsAsRawInDotNetByteOrder_WhenTheTypeIsGuid()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        OracleBinding binding = _numberBooleans.For(typeof(Guid), "Id");

        binding.DbType.Should().Be(OracleDbType.Raw);
        binding.Convert(guid).Should().BeEquivalentTo(guid.ToByteArray(), options => options.WithStrictOrdering());
        ((byte[])binding.Convert(guid))[0].Should().Be(0x33, "Guid.ToByteArray() swaps the first three groups");
    }

    [TestCase(true, (short)1)]
    [TestCase(false, (short)0)]
    public void For_ShouldBindBooleansAsNumber_WhenTheBindingIsNumber(bool value, short expected)
    {
        OracleBinding binding = _numberBooleans.For(typeof(bool?), "Flag");

        binding.DbType.Should().Be(OracleDbType.Int16);
        binding.Convert(value).Should().Be(expected);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void For_ShouldBindNativeBooleans_WhenTheBindingIsBoolean(bool value)
    {
        OracleBinding binding = new OracleTypeMap(OracleBooleanBinding.Boolean).For(typeof(bool), "Flag");

        binding.DbType.Should().Be(OracleDbType.Boolean);
        binding.Convert(value).Should().Be(value);
    }

    [TestCase(typeof(object))]
    [TestCase(typeof(Uri))]
    [TestCase(null)]
    public void For_ShouldThrowAMarkedNotSupportedException_WhenTheTypeCannotBeArrayBound(Type type)
    {
        Action action = () => _numberBooleans.For(type, "Payload\r\n");

        var thrown = action.Should().Throw<NotSupportedException>()
            .WithMessage("The source column 'Payload??' has type *, which the Oracle provider does not support.");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [Test]
    public void For_ShouldBindDateOnlyAsDateAtMidnight_WhenTheColumnIsDateOnly()
    {
        OracleBinding binding = _numberBooleans.For(typeof(DateOnly?), "Day");

        binding.DbType.Should().Be(OracleDbType.Date);
        binding.Convert(new DateOnly(2026, 11, 16)).Should().Be(new DateTime(2026, 11, 16, 0, 0, 0));
    }

    [Test]
    public void For_ShouldBindTimeOnlyAsAnInterval_WhenTheColumnIsTimeOnly()
    {
        OracleBinding binding = _numberBooleans.For(typeof(TimeOnly), "At");

        binding.DbType.Should().Be(OracleDbType.IntervalDS);
        binding.Convert(new TimeOnly(10, 30, 15)).Should().Be(new TimeSpan(10, 30, 15));
    }
}

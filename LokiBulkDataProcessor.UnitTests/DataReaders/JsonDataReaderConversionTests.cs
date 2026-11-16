using System;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Loki.BulkDataProcessor.DataReaders;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.DataReaders;

public class JsonDataReaderConversionTests
{
    private const string RowValue = "secret-row-value";

    private static JsonDataReader ReaderFor(JsonDocument document)
    {
        JsonDataReader reader = JsonDataReader.Create(document);
        reader.Read().Should().BeTrue();
        return reader;
    }

    private static JsonDocument Payload(string column, string type, string jsonValue) => JsonDocument.Parse(
        $"{{\"tableName\":\"T\",\"columns\":[{{\"name\":\"{column}\",\"type\":\"{type}\"}}],\"data\":[{{\"{column}\":{jsonValue}}}]}}");

    [TestCase("datetime")]
    [TestCase("date")]
    [TestCase("guid")]
    public void GetValue_ShouldThrowADataFreeFormatException_WhenAStringValueCannotBeParsed(string type)
    {
        using JsonDocument document = Payload("When", type, $"\"{RowValue}\"");
        using JsonDataReader reader = ReaderFor(document);

        Action action = () => reader.GetValue(0);

        var thrown = action.Should().Throw<FormatException>()
            .WithMessage($"A value in JSON column 'When' could not be converted to the declared type '{type}'.");
        thrown.Which.Message.Should().NotContain(RowValue);
        thrown.Which.InnerException.Should().BeNull("the BCL exception quotes the value, so it is not kept");
    }

    [TestCase("int")]
    [TestCase("decimal")]
    [TestCase("short")]
    public void GetValue_ShouldNotEchoTheValue_WhenANumericColumnHoldsAString(string type)
    {
        using JsonDocument document = Payload("Amount", type, $"\"{RowValue}\"");
        using JsonDataReader reader = ReaderFor(document);

        Action action = () => reader.GetValue(0);

        action.Should().Throw<FormatException>().Which.Message.Should().NotContain(RowValue);
    }

    [Test]
    public void GetValue_ShouldReturnTheParsedValue_WhenTheStringIsValid()
    {
        using JsonDocument document = Payload("When", "datetime", "\"2026-11-15T10:30:00\"");
        using JsonDataReader reader = ReaderFor(document);

        reader.GetValue(0).Should().Be(new DateTime(2026, 11, 15, 10, 30, 0));
    }

    [TestCase("decimal", "\"1,5\"")]
    [TestCase("int", "\"1,000\"")]
    [TestCase("double", "\"1.5\"")]
    public void GetValue_ShouldThrowADataFreeFormatException_WhenANumericColumnHoldsAString(string type, string jsonValue)
    {
        using JsonDocument document = Payload("Amount", type, jsonValue);
        using JsonDataReader reader = ReaderFor(document);

        Action action = () => reader.GetValue(0);

        action.Should().Throw<FormatException>().WithMessage($"A value in JSON column 'Amount' could not be converted to the declared type '{type}'.");
    }

    [Test]
    public void GetValue_ShouldParseDatesWithTheInvariantCulture_WhenTheHostCultureIsDayFirst()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            using JsonDocument document = Payload("When", "datetime", "\"03/04/2026\"");
            using JsonDataReader reader = ReaderFor(document);

            reader.GetValue(0).Should().Be(new DateTime(2026, 3, 4), "the invariant culture reads month first, whatever the host culture");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    [TestCase("\"TRUE\"", true)]
    [TestCase("\"false\"", false)]
    public void GetValue_ShouldReadTheBoolean_WhenTheValueIsTrueOrFalse(string jsonValue, bool expected)
    {
        using JsonDocument document = Payload("Flag", "bool", jsonValue);
        using JsonDataReader reader = ReaderFor(document);

        reader.GetValue(0).Should().Be(expected);
    }

    [TestCase("\"yes\"")]
    [TestCase("1")]
    [TestCase("0")]
    [TestCase("\"\"")]
    public void GetValue_ShouldThrowADataFreeFormatException_WhenABooleanIsNotTrueOrFalse(string jsonValue)
    {
        using JsonDocument document = Payload("Flag", "bool", jsonValue);
        using JsonDataReader reader = ReaderFor(document);

        Action action = () => reader.GetValue(0);

        action.Should().Throw<FormatException>().WithMessage("A value in JSON column 'Flag' could not be converted to the declared type 'bool'.");
    }

    [TestCase("int", "1e20")]
    [TestCase("short", "70000")]
    [TestCase("char", "5")]
    public void GetValue_ShouldThrowADataFreeFormatException_WhenANumberDoesNotFitTheDeclaredType(string type, string jsonValue)
    {
        using JsonDocument document = Payload("Amount", type, jsonValue);
        using JsonDataReader reader = ReaderFor(document);

        Action action = () => reader.GetValue(0);

        action.Should().Throw<FormatException>().Which.InnerException.Should().BeNull();
    }
}

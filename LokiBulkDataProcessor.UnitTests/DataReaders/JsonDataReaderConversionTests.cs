using System;
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

        action.Should().Throw<Exception>().Which.Message.Should().NotContain(RowValue);
    }

    [Test]
    public void GetValue_ShouldReturnTheParsedValue_WhenTheStringIsValid()
    {
        using JsonDocument document = Payload("When", "datetime", "\"2026-11-15T10:30:00\"");
        using JsonDataReader reader = ReaderFor(document);

        reader.GetValue(0).Should().Be(new DateTime(2026, 11, 15, 10, 30, 0));
    }
}

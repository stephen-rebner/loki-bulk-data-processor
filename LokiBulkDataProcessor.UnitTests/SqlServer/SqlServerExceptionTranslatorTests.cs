using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer.Internal;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.SqlServer;

public class SqlServerExceptionTranslatorTests
{
    private const string DocumentedMessage =
        "The given column mappings does not match up with any column in the destination table. Note the mappings are case sensitive.";

    private const string RowValue = "secret-row-value";

    private static readonly BulkWriteTarget Target = new("dbo.Posts", Array.Empty<ColumnMap>());

    [TestCase("The given ColumnMapping does not match up with any column in the source or destination.")]
    [TestCase("The given ColumnName 'Titel' does not match up with any column in data source.")]
    public void Translate_ShouldReturnTheDocumentedMessageWithTheOriginalAsInner_WhenTheColumnMappingDoesNotMatch(string driverMessage)
    {
        var original = new InvalidOperationException(driverMessage);

        Exception translated = SqlServerExceptionTranslator.Translate(original, Target);

        translated.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DocumentedMessage);
        translated.Message.Should().StartWith(BulkCopyErrorMessages.ColumnMappingMismatch);
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldUseAFixedMessageNamingTheTableAndKeepTheOriginalAsInner_WhenTheErrorIsNotASqlException()
    {
        var original = new FormatException("Cannot insert duplicate key");

        Exception translated = SqlServerExceptionTranslator.Translate(original, Target);

        translated.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Bulk copy to 'dbo.Posts' failed. See InnerException for details.");
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldNotUseTheMappingMessage_WhenTheErrorIsNotAnInvalidOperationException()
    {
        var original = new ArgumentException("The given ColumnName is odd");

        SqlServerExceptionTranslator.Translate(original, Target).Message.Should().NotBe(DocumentedMessage).And.NotContain("odd");
    }

    [Test]
    public void Translate_ShouldNotEchoTheRowValue_WhenTheDriverMessageContainsIt()
    {
        var original = new InvalidOperationException(
            $"The given value '{RowValue}' of type String from the data source cannot be converted to type int of the specified target column.");

        Exception translated = SqlServerExceptionTranslator.Translate(original, Target);

        translated.Message.Should().NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(original);
        translated.InnerException.Message.Should().Contain(RowValue, "the driver's message is kept for the caller on InnerException");
    }

    [Test]
    public void Translate_ShouldNotEchoTheDriverText_WhenAColumnMappingMessageNamesAValue()
    {
        var original = new InvalidOperationException($"The given ColumnName '{RowValue}' does not match up with any column in data source.");

        Exception translated = SqlServerExceptionTranslator.Translate(original, Target);

        translated.Message.Should().Be(DocumentedMessage);
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldSanitiseTheTableName_WhenItContainsControlCharacters()
    {
        var target = new BulkWriteTarget("Posts\r\nFORGED", Array.Empty<ColumnMap>());

        Exception translated = SqlServerExceptionTranslator.Translate(new FormatException("x"), target);

        translated.Message.Should().Be("Bulk copy to 'Posts??FORGED' failed. See InnerException for details.");
    }
}

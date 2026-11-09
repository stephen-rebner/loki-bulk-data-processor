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

    [TestCase("The given ColumnMapping does not match up with any column in the source or destination.")]
    [TestCase("The given ColumnName 'Titel' does not match up with any column in data source.")]
    public void Translate_ShouldReturnTheDocumentedMessageWithTheOriginalAsInner_WhenTheColumnMappingDoesNotMatch(string driverMessage)
    {
        var original = new InvalidOperationException(driverMessage);

        Exception translated = SqlServerExceptionTranslator.Translate(original);

        translated.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DocumentedMessage);
        translated.Message.Should().StartWith(BulkCopyErrorMessages.ColumnMappingMismatch);
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldWrapOtherErrorsKeepingTheirMessageAndTheOriginalAsInner()
    {
        var original = new FormatException("Cannot insert duplicate key");

        Exception translated = SqlServerExceptionTranslator.Translate(original);

        translated.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Cannot insert duplicate key");
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldNotUseTheMappingMessage_WhenTheErrorIsNotAnInvalidOperationException()
    {
        var original = new ArgumentException("The given ColumnName is odd");

        SqlServerExceptionTranslator.Translate(original).Message.Should().Be("The given ColumnName is odd");
    }
}

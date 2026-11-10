using FluentAssertions;
using Loki.BulkDataProcessor.Utils.Validation;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Utils;

public class MessageTextTests
{
    [TestCase(null, "")]
    [TestCase("Plain", "Plain")]
    [TestCase("a\r\nb\tc\0", "a??b?c?")]
    public void Sanitize_ShouldReplaceControlCharacters_IfAnyArePresent(string value, string expected)
    {
        MessageText.Sanitize(value).Should().Be(expected);
    }

    [Test]
    public void Sanitize_ShouldTruncate_WhenTheValueIsLongerThanMaxLength()
    {
        MessageText.Sanitize(new string('a', 500)).Should().Be(new string('a', MessageText.MaxLength) + "...");
        MessageText.Sanitize(new string('a', MessageText.MaxLength)).Should().Be(new string('a', MessageText.MaxLength));
    }
}

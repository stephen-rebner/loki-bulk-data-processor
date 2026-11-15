using FluentAssertions;
using Loki.BulkDataProcessor.Mappings.Resolution;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Mappings;

public class MappingKeyTests
{
    [TestCase("Source", "Destination", "Source")]
    [TestCase(null, "Destination", "Destination")]
    [TestCase("", "Destination", "Destination")]
    [TestCase("  ", "Destination", "Destination")]
    public void For_ShouldReturnTheSourceNameOrTheDestinationTableName_WhenTheSourceNameIsSetOrBlank(string sourceName, string destination, string expected)
    {
        MappingKey.For(sourceName, destination).Should().Be(expected);
    }
}

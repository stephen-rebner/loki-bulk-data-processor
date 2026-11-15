using System.Linq;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OraclePublicApiTests
{
    [Test]
    public void OracleAssembly_ShouldExportOnlyThePlannedPublicTypes_WhenBuilt()
    {
        string[] exported = typeof(OracleProvider).Assembly.GetExportedTypes().Select(t => t.FullName).ToArray();

        exported.Should().BeEquivalentTo(
            "Loki.BulkDataProcessor.Oracle.OracleProvider",
            "Loki.BulkDataProcessor.Oracle.OracleProviderOptions",
            "Loki.BulkDataProcessor.Oracle.OracleWriteMode",
            "Loki.BulkDataProcessor.Oracle.OracleIdentifierResolution",
            "Loki.BulkDataProcessor.Oracle.OracleBooleanBinding",
            "Loki.BulkDataProcessor.DependancyInjection.OracleBuilderExtensions");
    }

    [Test]
    public void OracleProviderName_ShouldBeOracle_WhenUsedWithTheFactory()
    {
        OracleProvider.Name.Should().Be("Oracle");
    }
}

using System;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Factory;

public class BulkProcessorFactoryTests
{
    private const string ConnectionString = "Server=fake;Password=secret";

    [Test]
    public void Create_ShouldReturnAProcessorForTheProvider_WhenTheNameMatchesIgnoringCase()
    {
        BulkProcessorFactory factory = FactoryFor("SqlServer", "Oracle");

        IBulkProcessor processor = factory.Create("sqlserver", ConnectionString);

        processor.ProviderName.Should().Be("SqlServer");
    }

    [Test]
    public void Create_ShouldReturnANewInstanceWithItsOwnSettings_OnEveryCall()
    {
        BulkProcessorFactory factory = FactoryFor("SqlServer");

        IBulkProcessor first = factory.Create("SqlServer", ConnectionString);
        IBulkProcessor second = factory.Create("SqlServer", ConnectionString);
        first.BatchSize = 1;

        second.Should().NotBeSameAs(first);
        second.BatchSize.Should().Be(5000);
    }

    [Test]
    public void Create_ShouldThrowArgumentExceptionListingTheRegisteredNames_WhenTheProviderIsUnknown()
    {
        BulkProcessorFactory factory = FactoryFor("SqlServer", "Oracle");

        Action action = () => factory.Create("PostgreSql", ConnectionString);

        action.Should().Throw<ArgumentException>()
            .WithMessage("No bulk copy provider named 'PostgreSql' is registered. Registered: SqlServer, Oracle.*")
            .Which.ParamName.Should().Be("providerName");
        action.Should().Throw<ArgumentException>().Which.Message.Should().NotContain("secret");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Create_ShouldThrowArgumentException_WhenTheProviderNameIsBlank(string providerName)
    {
        Action action = () => FactoryFor("SqlServer").Create(providerName, ConnectionString);

        action.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("providerName");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Create_ShouldThrowArgumentException_WhenTheConnectionStringIsBlank(string connectionString)
    {
        Action action = () => FactoryFor("SqlServer").Create("SqlServer", connectionString);

        action.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("connectionString");
    }

    [Test]
    public void Constructor_ShouldThrowInvalidOperationException_WhenTwoProvidersShareAName()
    {
        Action action = () => FactoryFor("SqlServer", "SQLSERVER");

        action.Should().Throw<InvalidOperationException>().WithMessage("Bulk copy provider 'SQLSERVER' is registered more than once.");
    }

    [Test]
    public void HasProvider_ShouldMatchIgnoringCase_AndBeFalseForUnknownOrNullNames()
    {
        BulkProcessorFactory factory = FactoryFor("SqlServer");

        factory.HasProvider("SQLSERVER").Should().BeTrue();
        factory.HasProvider("Oracle").Should().BeFalse();
        factory.HasProvider(null).Should().BeFalse();
    }

    [Test]
    public void ProviderNames_ShouldListEveryRegisteredProvider()
    {
        FactoryFor("SqlServer", "Oracle").ProviderNames.Should().BeEquivalentTo("SqlServer", "Oracle");
    }

    private static BulkProcessorFactory FactoryFor(params string[] providerNames)
    {
        var providers = Array.ConvertAll(providerNames, name => new FakeProvider(name).Object);
        return new BulkProcessorFactory(providers, MappingRegistry.Empty, LokiLogging.None);
    }
}

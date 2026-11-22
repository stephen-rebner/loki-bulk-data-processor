using System;
using System.Linq;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleRegistrationTests
{
    private const string ConnectionString = "User Id=loki;Password=fake;Data Source=fake:1521/FREEPDB1";

    [Test]
    public void AddOracle_ShouldRegisterTheOracleProviderWithTheFactory_WhenChained()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddOracle();
        using ServiceProvider provider = services.BuildServiceProvider();

        IBulkProcessorFactory factory = provider.GetRequiredService<IBulkProcessorFactory>();

        factory.HasProvider("Oracle").Should().BeTrue();
        factory.HasProvider("oracle").Should().BeTrue();
        factory.ProviderNames.Should().BeEquivalentTo(OracleProvider.Name);
        factory.Create(OracleProvider.Name, ConnectionString).ProviderName.Should().Be("Oracle");
    }

    [Test]
    public void AddOracle_ShouldUseTheDefaultOptions_WhenNoConfigureCallbackIsGiven()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddOracle();
        using ServiceProvider provider = services.BuildServiceProvider();

        var oracle = (OracleBulkCopyProvider)provider.GetServices<IBulkCopyProvider>().Single();

        oracle.Options.WriteMode.Should().Be(OracleWriteMode.ArrayBind);
        oracle.Options.IdentifierResolution.Should().Be(OracleIdentifierResolution.Unquoted);
        oracle.Options.BooleanBinding.Should().Be(OracleBooleanBinding.Number);
    }

    [Test]
    public void AddOracle_ShouldRegisterEachNamedConfigurationWithItsOwnOptions_WhenCalledWithDifferentNames()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor()
            .AddOracle()
            .AddOracle(o => o.IdentifierResolution = OracleIdentifierResolution.CatalogLookup, "Oracle.Catalog")
            .AddSqlServer();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBulkProcessorFactory>().ProviderNames
            .Should().BeEquivalentTo("Oracle", "Oracle.Catalog", SqlServerProvider.Name);
        var catalog = provider.GetServices<IBulkCopyProvider>().OfType<OracleBulkCopyProvider>().Single(p => p.Name == "Oracle.Catalog");
        catalog.Options.IdentifierResolution.Should().Be(OracleIdentifierResolution.CatalogLookup);
    }

    [Test]
    public void AddOracle_ShouldFailOnFactoryResolution_WhenTheSameNameIsRegisteredTwice()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddOracle().AddOracle();
        using ServiceProvider provider = services.BuildServiceProvider();

        Action action = () => provider.GetRequiredService<IBulkProcessorFactory>();

        action.Should().Throw<InvalidOperationException>().WithMessage("*'Oracle' is registered more than once*");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void AddOracle_ShouldThrowArgumentException_WhenTheNameIsBlank(string name)
    {
        var builder = new ServiceCollection().AddLokiBulkDataProcessor();

        Action action = () => builder.AddOracle(name: name);

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void AddOracle_ShouldThrowArgumentNullException_WhenTheBuilderIsNull()
    {
        Action action = () => OracleBuilderExtensions.AddOracle(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void AddOracle_ShouldRegisterADirectPathProvider_WhenDirectPathIsSelected()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddOracle(o => o.WriteMode = OracleWriteMode.DirectPath, "Oracle.DirectPath");
        using ServiceProvider provider = services.BuildServiceProvider();

        var oracle = (OracleBulkCopyProvider)provider.GetServices<IBulkCopyProvider>().Single();

        oracle.Name.Should().Be("Oracle.DirectPath");
        oracle.Options.WriteMode.Should().Be(OracleWriteMode.DirectPath);
        provider.GetRequiredService<IBulkProcessorFactory>().HasProvider("Oracle.DirectPath").Should().BeTrue();
    }

    [Test]
    public void AddOracle_ShouldThrowArgumentOutOfRange_WhenAnOptionIsUndefined()
    {
        var builder = new ServiceCollection().AddLokiBulkDataProcessor();

        Action action = () => builder.AddOracle(o => o.BooleanBinding = (OracleBooleanBinding)99);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void AddOracle_ShouldIgnoreLaterChangesToTheOptions_WhenTheCallerKeepsAReference()
    {
        OracleProviderOptions captured = null;
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddOracle(o => captured = o);
        captured.IdentifierResolution = OracleIdentifierResolution.CatalogLookup;
        using ServiceProvider provider = services.BuildServiceProvider();

        var oracle = (OracleBulkCopyProvider)provider.GetServices<IBulkCopyProvider>().Single();

        oracle.Options.IdentifierResolution.Should().Be(OracleIdentifierResolution.Unquoted);
    }
}

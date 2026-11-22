using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Errors;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace Loki.Samples.WebApi.Tests;

public class EngineCatalogTests
{
    private const string SqlServerConnectionString = "sql-connection-string";
    private const string OracleConnectionString = "oracle-connection-string";

    private Mock<IBulkProcessorFactory> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new Mock<IBulkProcessorFactory>();
        _factory.Setup(f => f.HasProvider(It.IsAny<string>())).Returns(true);
    }

    // DatabaseEngine is internal, so the test cases name it (a public test method cannot take an internal type).
    [TestCase("sqlserver", nameof(DatabaseEngine.SqlServer))]
    [TestCase("SqlServer", nameof(DatabaseEngine.SqlServer))]
    [TestCase("oracle", nameof(DatabaseEngine.Oracle))]
    [TestCase("ORACLE", nameof(DatabaseEngine.Oracle))]
    public void Parse_ShouldReturnTheEngine_WhenTheRouteValueIsKnown(string routeValue, string expected)
    {
        EngineCatalog.Parse(routeValue).Should().Be(Enum.Parse<DatabaseEngine>(expected));
    }

    [TestCase("postgresql")]
    [TestCase("0")]          // Enum.TryParse would accept a number; the catalog does not
    [TestCase("Oracle.DirectPath")]
    [TestCase("")]
    public void Parse_ShouldThrowNotFound_WhenTheRouteValueIsNotAnEngine(string routeValue)
    {
        Action parse = () => EngineCatalog.Parse(routeValue);

        parse.Should().Throw<RequestRejectedException>()
            .Where(e => e.StatusCode == 404 && (routeValue.Length == 0 || !e.Message.Contains(routeValue)));
    }

    [TestCase(nameof(DatabaseEngine.SqlServer), "SqlServer", SqlServerConnectionString)]
    [TestCase(nameof(DatabaseEngine.Oracle), "Oracle", OracleConnectionString)]
    public void CreateProcessor_ShouldUseTheEnginesProviderAndConfiguredConnectionString_WhenTheEngineIsConfigured(
        string engine, string providerName, string connectionString)
    {
        IBulkProcessor processor = Mock.Of<IBulkProcessor>();
        _factory.Setup(f => f.Create(providerName, connectionString)).Returns(processor);

        CatalogWith(SqlServerConnectionString, OracleConnectionString).CreateProcessor(Enum.Parse<DatabaseEngine>(engine))
            .Should().BeSameAs(processor);
    }

    [Test]
    public void CreateOracleDirectPathProcessor_ShouldUseTheDirectPathProviderAndTheOracleConnectionString_WhenRegistered()
    {
        IBulkProcessor processor = Mock.Of<IBulkProcessor>();
        _factory.Setup(f => f.Create(SampleProviders.OracleDirectPath, OracleConnectionString)).Returns(processor);

        CatalogWith(SqlServerConnectionString, OracleConnectionString).CreateOracleDirectPathProcessor().Should().BeSameAs(processor);
    }

    [Test]
    public void CreateProcessor_ShouldThrowUnavailable_WhenTheProviderIsNotRegistered()
    {
        _factory.Setup(f => f.HasProvider("Oracle")).Returns(false);

        Action create = () => CatalogWith(SqlServerConnectionString, OracleConnectionString).CreateProcessor(DatabaseEngine.Oracle);

        create.Should().Throw<RequestRejectedException>().Where(e => e.StatusCode == 503);
        _factory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void CreateProcessor_ShouldThrowUnavailableWithoutCreating_WhenTheConnectionStringIsBlank(string? oracleConnectionString)
    {
        Action create = () => CatalogWith(SqlServerConnectionString, oracleConnectionString).CreateProcessor(DatabaseEngine.Oracle);

        create.Should().Throw<RequestRejectedException>().Where(e => e.StatusCode == 503);
        _factory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void IsConfigured_ShouldBeFalse_WhenTheConnectionStringIsMissing()
    {
        EngineCatalog catalog = CatalogWith(SqlServerConnectionString, oracleConnectionString: null);

        catalog.IsConfigured(DatabaseEngine.SqlServer).Should().BeTrue();
        catalog.IsConfigured(DatabaseEngine.Oracle).Should().BeFalse();
    }

    private EngineCatalog CatalogWith(string? sqlServerConnectionString, string? oracleConnectionString)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = sqlServerConnectionString,
                ["ConnectionStrings:Oracle"] = oracleConnectionString
            })
            .Build();
        return new EngineCatalog(_factory.Object, configuration);
    }
}

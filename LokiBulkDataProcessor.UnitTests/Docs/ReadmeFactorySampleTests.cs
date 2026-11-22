using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Docs;

// Doc-snippet test for README.md, section "Choosing the database at run time (IBulkProcessorFactory)".
// The registration, DatabaseEngine, SeedWriter and the Moq test below are the README sample, character for character.
// If you change one, change the other: this test is what proves the README sample compiles and works.

// README sample: begin
public enum DatabaseEngine { SqlServer, Oracle, PostgreSql }

public sealed class SeedWriter(IBulkProcessorFactory bulkProcessorFactory)
{
    public async Task WriteAsync(DatabaseEngine engine, string connectionString, DataTable rows, CancellationToken ct)
    {
        string provider = engine.ToString();                  // "SqlServer", "Oracle", "PostgreSql"
        if (!bulkProcessorFactory.HasProvider(provider))
        {
            throw new NotSupportedException($"Bulk seeding is not supported for {provider}.");
        }

        IBulkProcessor processor = bulkProcessorFactory.Create(provider, connectionString);
        processor.BatchSize = 2000;
        await processor.SaveAsync(rows, rows.TableName, ct);
    }
}

public class SeedWriterTests
{
    [Test]
    public async Task WriteAsync_ShouldSaveThroughTheFactoryProcessor_WhenTheEngineIsSqlServer()
    {
        Mock<IBulkProcessor> processor = new();
        Mock<IBulkProcessorFactory> factory = new();
        factory.Setup(f => f.HasProvider("SqlServer")).Returns(true);
        factory.Setup(f => f.Create("SqlServer", "cs")).Returns(processor.Object);
        DataTable rows = new("Customers");

        await new SeedWriter(factory.Object).WriteAsync(DatabaseEngine.SqlServer, "cs", rows, CancellationToken.None);

        processor.VerifySet(p => p.BatchSize = 2000);
        processor.Verify(p => p.SaveAsync(rows, "Customers", It.IsAny<CancellationToken>()), Times.Once);
    }
}
// README sample: end

public class ReadmeFactorySampleTests
{
    [Test]
    public void Registration_ShouldRegisterEveryEngineTheSampleUses_WhenConfiguredAsInTheReadme()
    {
        var services = new ServiceCollection();

        // README sample (registration): begin
        services.AddLokiBulkDataProcessor()
                .AddSqlServer()                                // "SqlServer"
                .AddOracle();                                  // "Oracle": array bind (the default)
        services.AddScoped<SeedWriter>();
        // README sample (registration): end

        using ServiceProvider provider = services.BuildServiceProvider();
        IBulkProcessorFactory factory = provider.GetRequiredService<IBulkProcessorFactory>();

        factory.HasProvider(DatabaseEngine.SqlServer.ToString()).Should().BeTrue();
        factory.HasProvider(DatabaseEngine.Oracle.ToString()).Should().BeTrue();
        factory.HasProvider(DatabaseEngine.PostgreSql.ToString()).Should().BeFalse();
        DatabaseEngine.SqlServer.ToString().Should().Be(SqlServerProvider.Name);
        DatabaseEngine.Oracle.ToString().Should().Be(OracleProvider.Name);
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SeedWriter>().Should().NotBeNull();
    }

    [Test]
    public async Task WriteAsync_ShouldThrowNotSupported_WhenTheEngineHasNoProvider()
    {
        Mock<IBulkProcessorFactory> factory = new();
        factory.Setup(f => f.HasProvider("PostgreSql")).Returns(false);

        Func<Task> act = () => new SeedWriter(factory.Object)
            .WriteAsync(DatabaseEngine.PostgreSql, "cs", new DataTable("Customers"), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*PostgreSql*");
        factory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

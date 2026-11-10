using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Logging;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.DependencyInjection;

public class RegistrationTests
{
    private const string ConnectionString = "Server=fake;Database=fake";

    [Test]
    public void AddLokiBulkDataProcessor_ShouldRegisterASingletonFactoryWithTheSqlServerProvider_WhenAddSqlServerIsChained()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddSqlServer();
        using ServiceProvider provider = services.BuildServiceProvider();

        IBulkProcessorFactory factory = provider.GetRequiredService<IBulkProcessorFactory>();

        factory.Should().BeSameAs(provider.GetRequiredService<IBulkProcessorFactory>());
        factory.ProviderNames.Should().BeEquivalentTo(SqlServerProvider.Name);
        factory.Create(SqlServerProvider.Name, ConnectionString).ProviderName.Should().Be("SqlServer");
    }

    [Test]
    public void AddSqlServer_ShouldRegisterEachNamedConfiguration_WhenCalledWithDifferentNames()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddSqlServer().AddSqlServer("SqlServer.Reporting");
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBulkProcessorFactory>().ProviderNames
            .Should().BeEquivalentTo("SqlServer", "SqlServer.Reporting");
    }

    [Test]
    public void AddSqlServer_ShouldFailOnFactoryResolution_WhenTheSameNameIsRegisteredTwice()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddSqlServer().AddSqlServer();
        using ServiceProvider provider = services.BuildServiceProvider();

        Action action = () => provider.GetRequiredService<IBulkProcessorFactory>();

        action.Should().Throw<InvalidOperationException>().WithMessage("*'SqlServer' is registered more than once*");
    }

    [TestCase(null)]
    [TestCase("")]
    public void AddSqlServer_ShouldThrow_WhenTheNameIsBlank(string name)
    {
        var builder = new ServiceCollection().AddLokiBulkDataProcessor();

        Action action = () => builder.AddSqlServer(name);

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void LegacyAddLokiBulkDataProcessor_ShouldResolveScopedProcessorsWithIndependentSettings_WhenResolvedFromTwoScopes()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(ConnectionString);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope1 = provider.CreateScope();
        using IServiceScope scope2 = provider.CreateScope();

        IBulkProcessor first = scope1.ServiceProvider.GetRequiredService<IBulkProcessor>();
        IBulkProcessor second = scope2.ServiceProvider.GetRequiredService<IBulkProcessor>();
        first.BatchSize = 1;
        first.Timeout = 2;

        first.Should().BeSameAs(scope1.ServiceProvider.GetRequiredService<IBulkProcessor>());
        second.Should().NotBeSameAs(first);
        second.BatchSize.Should().Be(5000);
        second.Timeout.Should().Be(30);
        first.ProviderName.Should().Be(SqlServerProvider.Name);
    }

    [Test]
    public void LegacyAddLokiBulkDataProcessor_ShouldAlsoRegisterTheFactory_WhenCalled()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(ConnectionString, mappingAssembly: null);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBulkProcessorFactory>().HasProvider(SqlServerProvider.Name).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void LegacyAddLokiBulkDataProcessor_ShouldThrowArgumentException_WhenTheConnectionStringIsBlank(string connectionString)
    {
        Action action = () => new ServiceCollection().AddLokiBulkDataProcessor(connectionString, null);

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void AddLokiBulkDataProcessor_ShouldThrowInvalidOperationException_WhenCalledTwice()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(ConnectionString);

        Action builderOverload = () => services.AddLokiBulkDataProcessor();
        Action legacyOverload = () => services.AddLokiBulkDataProcessor(ConnectionString);

        builderOverload.Should().Throw<InvalidOperationException>().WithMessage("AddLokiBulkDataProcessor has already been called*");
        legacyOverload.Should().Throw<InvalidOperationException>().WithMessage("AddLokiBulkDataProcessor has already been called*");
    }

    [Test]
    public void AddLokiBulkDataProcessor_ShouldNotRegisterAnAppWideLoggerFactory_WhenAnyLoggingOptionIsUsed()
    {
        var withFactory = new ServiceCollection();
        withFactory.AddLokiBulkDataProcessor(ConnectionString, null, new RecordingLoggerFactory());
        var withConsole = new ServiceCollection();
        withConsole.AddLokiBulkDataProcessor(ConnectionString, null, null, o => o.EnableConsoleLogging = true);
        var withNothing = new ServiceCollection();
        withNothing.AddLokiBulkDataProcessor(ConnectionString);

        foreach (var services in new[] { withFactory, withConsole, withNothing })
        {
            services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(ILoggerFactory));
            services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(ILoggerProvider));
        }
    }

    [Test]
    public void LegacyAddLokiBulkDataProcessor_ShouldLogThroughTheGivenLoggerFactory_WhenOneIsGiven()
    {
        var loggerFactory = new RecordingLoggerFactory();
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(ConnectionString, null, loggerFactory);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.CreateScope().ServiceProvider.GetRequiredService<IBulkProcessor>().BatchSize = 10;

        loggerFactory.Contains(LogLevel.Information, "Setting BatchSize").Should().BeTrue();
    }

    [Test]
    public void AddLokiBulkDataProcessor_ShouldUseTheHostLoggerFactoryOnly_WhenOptedIn()
    {
        var hostFactory = new RecordingLoggerFactory();

        LoggerFactoryUsedBy(hostFactory, useHost: false).Should().BeSameAs(NullLoggerFactory.Instance);
        LoggerFactoryUsedBy(hostFactory, useHost: true).Should().BeSameAs(hostFactory);
    }

    [Test]
    public void Select_ShouldPreferExplicitThenConsoleThenHostThenNone_WhenSeveralLoggerSourcesAreAvailable()
    {
        var explicitFactory = new RecordingLoggerFactory();
        var hostFactory = new RecordingLoggerFactory();
        var console = new LokiLoggingOptions { EnableConsoleLogging = true };
        IServiceProvider host = new ServiceCollection().AddSingleton<ILoggerFactory>(hostFactory).BuildServiceProvider();

        LokiLoggingSelector.Select(explicitFactory, console, true, host).LoggerFactory.Should().BeSameAs(explicitFactory);
        using (LokiLogging consoleLogging = LokiLoggingSelector.Select(null, console, true, host))
        {
            consoleLogging.LoggerFactory.Should().NotBeSameAs(hostFactory).And.NotBeSameAs(NullLoggerFactory.Instance);
        }

        LokiLoggingSelector.Select(null, new LokiLoggingOptions(), true, host).LoggerFactory.Should().BeSameAs(hostFactory);
        LokiLoggingSelector.Select(null, null, false, host).LoggerFactory.Should().BeSameAs(NullLoggerFactory.Instance);
    }

    [Test]
    public void Dispose_ShouldDisposeTheLoggerFactoryOnly_IfLokiLoggingOwnsIt()
    {
        var owned = new RecordingLoggerFactory();
        var borrowed = new RecordingLoggerFactory();

        new LokiLogging(owned, ownsLoggerFactory: true).Dispose();
        new LokiLogging(borrowed).Dispose();

        owned.IsDisposed.Should().BeTrue();
        borrowed.IsDisposed.Should().BeFalse();
    }

    [Test]
    public void AddLokiBulkDataProcessor_ShouldRegisterTheProviderSpiAsASingleton_WhenAProviderIsAdded()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor().AddSqlServer();

        services.Single(descriptor => descriptor.ServiceType == typeof(IBulkCopyProvider)).Lifetime.Should().Be(ServiceLifetime.Singleton);
        services.Single(descriptor => descriptor.ServiceType == typeof(IBulkProcessorFactory)).Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Test]
    public void AddLokiBulkDataProcessor_ShouldRegisterTheJsonPolicyFromTheOptions_WhenTheyAreDefaultOrSet()
    {
        JsonPolicyFor(_ => { }).Should().Be(JsonPayloadPolicy.Default);
        JsonPolicyFor(o => { o.RequireDataMappingForJson = true; o.MaxJsonPayloadBytes = 1024; })
            .Should().Be(new JsonPayloadPolicy(RequireDataMapping: true, MaxPayloadBytes: 1024));
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    public void MaxJsonPayloadBytes_ShouldThrow_IfValueSetIsNotPositive(long value)
    {
        Action action = () => new LokiBulkDataProcessorOptions().MaxJsonPayloadBytes = value;

        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("MaxJsonPayloadBytes");
    }

    [Test]
    public async Task FactoryProcessors_ShouldEnforceRequireDataMappingForJson_WhenEnabledInTheOptions()
    {
        var fake = new FakeProvider("Fake");
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(o => o.RequireDataMappingForJson = true).Services.AddSingleton(fake.Object);
        using ServiceProvider provider = services.BuildServiceProvider();
        IBulkProcessor processor = provider.GetRequiredService<IBulkProcessorFactory>().Create("Fake", ConnectionString);

        Func<Task> action = () => processor.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"tableName\":\"T\",\"columns\":[{\"name\":\"A\",\"type\":\"int\"}],\"data\":[{\"A\":1}]}")));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RequireDataMappingForJson*");
        fake.Connection.OpenCount.Should().Be(0);
    }

    [Test]
    public void LegacyAddLokiBulkDataProcessor_ShouldHideTheConnectionStringInTheScopedFactoryDelegate_WhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(ConnectionString);

        object holder = services.Single(descriptor => descriptor.ServiceType == typeof(IBulkProcessor)).ImplementationFactory.Target;
        FieldInfo[] fields = holder.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => Equals(field.GetValue(holder), ConnectionString)).ToArray();

        fields.Should().NotBeEmpty();
        fields.Should().OnlyContain(field => field.GetCustomAttribute<DebuggerBrowsableAttribute>().State == DebuggerBrowsableState.Never);
        holder.ToString().Should().NotContain("fake");
        holder.GetType().GetCustomAttribute<DebuggerDisplayAttribute>().Value.Should().NotContain("{");
    }

    private static JsonPayloadPolicy JsonPolicyFor(Action<LokiBulkDataProcessorOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(configure).AddSqlServer();
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<JsonPayloadPolicy>();
    }

    private static ILoggerFactory LoggerFactoryUsedBy(ILoggerFactory hostFactory, bool useHost)
    {
        var services = new ServiceCollection();
        services.AddSingleton(hostFactory);
        services.AddLokiBulkDataProcessor(options => options.UseHostLoggerFactory = useHost).AddSqlServer();
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<LokiLogging>().LoggerFactory;
    }
}

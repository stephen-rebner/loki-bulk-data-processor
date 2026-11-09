using System;
using System.Linq;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Logging;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.DependancyInjection;

/// <summary>Registers Loki's provider-neutral services. Add providers with the returned builder.</summary>
public static class LokiServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IBulkProcessorFactory"/> and the mappings. Chain a provider, for example
    /// <c>services.AddLokiBulkDataProcessor(o =&gt; o.AddMappingsFrom(assembly)).AddSqlServer()</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Loki is already registered, or the mappings are invalid or duplicated.</exception>
    public static ILokiBulkDataProcessorBuilder AddLokiBulkDataProcessor(
        this IServiceCollection services, Action<LokiBulkDataProcessorOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfAlreadyRegistered(services);

        LokiBulkDataProcessorOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(new MappingRegistry(options.MappingAssemblies));
        RegisterLogging(services, options);
        services.AddSingleton<IBulkProcessorFactory, BulkProcessorFactory>();
        return new LokiBulkDataProcessorBuilder(services);
    }

    private static void ThrowIfAlreadyRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(MappingRegistry)))
        {
            throw new InvalidOperationException(
                "AddLokiBulkDataProcessor has already been called on this service collection. Combine providers on one builder instead.");
        }
    }

    private static void RegisterLogging(IServiceCollection services, LokiBulkDataProcessorOptions options)
    {
        ILoggerFactory explicitFactory = options.LoggerFactory;
        LokiLoggingOptions console = ReadConsoleOptions(options.ConsoleLogging);
        bool useHost = options.UseHostLoggerFactory;
        services.AddSingleton(sp => LokiLoggingSelector.Select(explicitFactory, console, useHost, sp));
    }

    private static LokiLoggingOptions ReadConsoleOptions(Action<LokiLoggingOptions> configure)
    {
        if (configure is null)
        {
            return null;
        }

        LokiLoggingOptions console = new();
        configure(console);
        return console;
    }

    private sealed class LokiBulkDataProcessorBuilder(IServiceCollection services) : ILokiBulkDataProcessorBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}

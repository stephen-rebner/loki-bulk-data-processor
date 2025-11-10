using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Core.Context;
using Loki.BulkDataProcessor.Core.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Reflection;
using LokiBulkDataProcessor.Core.Interfaces;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(
            this IServiceCollection services,
            string connectionString,
            Assembly mappingAssembly = null,
            ILoggerFactory loggerFactory = null,
            Action<LokiLoggingOptions> configureLogging = null)
        {
            // Setup logging only if explicitly requested
            if (loggerFactory == null)
            {
                // Only configure logging if explicitly requested
                if (configureLogging != null)
                {
                    var loggingOptions = new LokiLoggingOptions();
                    configureLogging.Invoke(loggingOptions);

                    services.AddLogging(builder =>
                    {
                        if (loggingOptions.EnableConsoleLogging)
                        {
                            builder.AddConsole();
                        }
                        builder.SetMinimumLevel(loggingOptions.MinimumLogLevel);
                    });
                }
                else
                {
                    // Register NullLoggerFactory when no logger is configured
                    services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
                }
            }
            else
            {
                // Use the provided logger factory
                services.AddSingleton(loggerFactory);
            }

            // Register dependencies
            services.AddSingleton<IAppContext, Core.Context.AppContext>(_ =>
                new Core.Context.AppContext(connectionString, new ModelMappings(mappingAssembly), new DataMappings(mappingAssembly)));

            services.AddTransient<ILokiDbConnection, LokiDbConnection>(provider =>
                new LokiDbConnection(provider.GetRequiredService<IAppContext>()));

            // Register command implementations
            services.AddScoped<IBulkModelsCommand, BulkCopyModelsCommand>();
            services.AddScoped<IBulkDataTableCommand, BulkCopyDataTableCommand>();
            services.AddScoped<IBulkCopyFromDataReaderCommand, BulkCopyFromDataReaderCommand>();

            // Register the command factory with logger support
            services.AddScoped<ICommandFactory, CommandFactory>(provider =>
                new CommandFactory(
                    provider.GetRequiredService<IAppContext>(),
                    provider.GetRequiredService<ILokiDbConnection>(),
                    provider.GetRequiredService<ILoggerFactory>()));

            // Register BulkProcessor with logger support
            services.AddScoped<IBulkProcessor, BulkProcessor>((provider) =>
                new BulkProcessor(
                    provider.GetRequiredService<ICommandFactory>(),
                    provider.GetRequiredService<IAppContext>(),
                    provider.GetRequiredService<ILoggerFactory>().CreateLogger<BulkProcessor>()));

            return services;
        }
    }
}
using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using AppContext = Loki.BulkDataProcessor.Context.AppContext;

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
            var loggingOptions = new LokiLoggingOptions();
            configureLogging?.Invoke(loggingOptions);

            // Setup logging
            if (loggerFactory == null)
            {
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
                services.AddSingleton(loggerFactory);
            }

            // Register dependencies
            services.AddSingleton<IAppContext, AppContext>(_ => 
                new AppContext(connectionString, new ModelMappings(mappingAssembly), new DataMappings(mappingAssembly)));

            services.AddTransient<ILokiDbConnection, LokiDbConnection>(provider => 
                new LokiDbConnection(provider.GetRequiredService<IAppContext>()));

            // Register command implementations
            services.AddTransient<IBulkModelsCommand, BulkCopyModelsCommand>();
            services.AddTransient<IBulkDataTableCommand, BulkCopyDataTableCommand>();
            services.AddTransient<IBulkCopyFromDataReaderCommand, BulkCopyFromDataReaderCommand>();
            
            // Register the command factory with logger support
            services.AddSingleton<ICommandFactory, CommandFactory>(provider => 
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
using System;
using System.Reflection;
using Loki.BulkDataProcessor.Logging;
using Loki.BulkDataProcessor.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    /// <summary>The single-database SQL Server registration kept from 8.x.</summary>
    public static class ServiceCollectionExtension
    {
        /// <summary>
        /// Registers Loki with the SQL Server provider and a scoped <see cref="IBulkProcessor"/> for
        /// <paramref name="connectionString"/>. Each DI scope gets its own processor and settings.
        /// Also registers <see cref="IBulkProcessorFactory"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">SQL Server connection string used by every scoped processor.</param>
        /// <param name="mappingAssembly">Assembly containing ModelMapping/DataMapping classes, or null for none.</param>
        /// <param name="loggerFactory">Logger factory used by Loki only. It is not registered for the rest of the app.</param>
        /// <param name="configureLogging">Loki-private console logging, used when <paramref name="loggerFactory"/> is null.</param>
        public static IServiceCollection AddLokiBulkDataProcessor(
            this IServiceCollection services,
            string connectionString,
            Assembly mappingAssembly = null,
            ILoggerFactory loggerFactory = null,
            Action<LokiLoggingOptions> configureLogging = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            services.AddLokiBulkDataProcessor(options => Configure(options, mappingAssembly, loggerFactory, configureLogging))
                .AddSqlServer();

            services.AddScoped<IBulkProcessor>(sp =>
                sp.GetRequiredService<IBulkProcessorFactory>().Create(SqlServerProvider.Name, connectionString));

            return services;
        }

        private static void Configure(LokiBulkDataProcessorOptions options, Assembly mappingAssembly,
            ILoggerFactory loggerFactory, Action<LokiLoggingOptions> configureLogging)
        {
            if (mappingAssembly is not null)
            {
                options.AddMappingsFrom(mappingAssembly);
            }

            options.LoggerFactory = loggerFactory;

            if (configureLogging is not null)
            {
                options.UseConsoleLogging(configureLogging);
            }
        }
    }
}

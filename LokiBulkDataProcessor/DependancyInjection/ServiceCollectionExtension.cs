using System;
using System.Diagnostics;
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
        /// <remarks>
        /// The scoped <see cref="IBulkProcessor"/> is not thread-safe and its settings (BatchSize, Timeout, Transaction)
        /// belong to that instance. If a singleton or other long-lived service captures it, every request shares those
        /// settings. Long-lived owners should inject <see cref="IBulkProcessorFactory"/> and call
        /// <see cref="IBulkProcessorFactory.Create"/> per unit of work instead. The JSON options
        /// (<c>RequireDataMappingForJson</c>, <c>MaxJsonPayloadBytes</c>) are only available through the
        /// <c>AddLokiBulkDataProcessor(Action&lt;LokiBulkDataProcessorOptions&gt;)</c> builder overload.
        /// </remarks>
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

            services.AddScoped<IBulkProcessor>(new LegacyProcessorSource(connectionString).Create);

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

        /// <summary>
        /// Holds the connection string for the scoped factory delegate. A lambda would capture it in a compiler-generated
        /// closure that the debugger shows; this type hides it from debugger views and <see cref="ToString"/>.
        /// </summary>
        [DebuggerDisplay("SQL Server processor source")]
        private sealed class LegacyProcessorSource(string connectionString)
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly string _connectionString = connectionString;

            public IBulkProcessor Create(IServiceProvider services) =>
                services.GetRequiredService<IBulkProcessorFactory>().Create(SqlServerProvider.Name, _connectionString);

            public override string ToString() => "SQL Server processor source";
        }
    }
}

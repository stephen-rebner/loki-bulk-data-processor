using Loki.BulkDataProcessor.Commands;
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Reflection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services, string connectionString, Assembly mappingAssembly = null)
        {
            services.AddSingleton<IAppContext, AppContext>(x => new AppContext(connectionString, new ModelMappings(mappingAssembly), new DataTableMappings(mappingAssembly)));
            services.AddTransient<ILokiDbConnection, LokiDbConnection>();
            services.AddTransient<IBulkModelsCommand, BulkCopyModelsCommand>();
            services.AddTransient<IBulkDataTableCommand, BulkCopyDataTableCommand>();
            services.AddTransient<IDbConnection, LokiDbConnection>();
            services.AddSingleton<ICommandFactory, CommandFactory>();
            services.AddScoped<IBulkProcessor, BulkProcessor>();
            return services;
        }
    }
}
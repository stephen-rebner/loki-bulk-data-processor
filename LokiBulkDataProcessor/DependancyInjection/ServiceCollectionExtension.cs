using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.Mappings.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services, string connectionString)
        {
            services.AddScoped<IBulkProcessor, BulkProcessor>(x => new BulkProcessor(connectionString, new CommandFactory()));
            return services;
        }

        public static IServiceCollection AddMappingsForAssembly(this IServiceCollection services, Assembly assembly)
        {
            services.AddSingleton<IMappingCollection, MappingCollection>(
                x => new MappingCollection(assembly));
            return services;
        }
    }
}
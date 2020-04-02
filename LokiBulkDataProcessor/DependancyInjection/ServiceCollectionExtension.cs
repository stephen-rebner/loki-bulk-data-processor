using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Context;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services, string connectionString, Assembly mappingAssembly = null)
        {
            services.AddSingleton<IAppContext, AppContext>(x => new AppContext(connectionString, new ModelMappingCollection(mappingAssembly), new DataTableMappingCollection(mappingAssembly)));
            services.AddSingleton<ICommandFactory, CommandFactory>();
            services.AddScoped<IBulkProcessor, BulkProcessor>();
            return services;
        }
    }
}
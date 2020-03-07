using Loki.BulkDataProcessor.Commands.Factory;
using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services, string connectionString)
        {
            services.AddScoped<IBulkProcessor, BulkProcessor>(x => new BulkProcessor(connectionString, new CommandFactory()));
            return services;
        }
    }
}
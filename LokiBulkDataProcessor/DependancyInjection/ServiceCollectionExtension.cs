using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services)
        {
            services.AddScoped<IBulkProcessor, BulkProcessor>();
            return services;
        }
    }
}
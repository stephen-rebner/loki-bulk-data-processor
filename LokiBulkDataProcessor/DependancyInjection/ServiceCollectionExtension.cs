using Loki.BulkDataProcessor.InternalDbOperations;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddLokiBulkDataProcessor(this IServiceCollection services, string connectionString)
        {
            services.AddScoped<ITempTable, TempTable>();
            services.AddScoped<IBulkProcessor, BulkProcessor>(x => new BulkProcessor(connectionString));
            return services;
        }
    }
}
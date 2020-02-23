using Loki.BulkDataProcessor.Context;
using Loki.BulkDataProcessor.Context.Interface;
using Loki.BulkDataProcessor.Constants;
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

            services.AddSingleton<IModelDbContext, ModelDbContext>(dbContext => new ModelDbContext(
                connectionString, 
                DefaultConfigValues.BatchSize, 
                DefaultConfigValues.Timeout));

            //services.AddScoped<IBulkProcessor, BulkProcessor>(x => new BulkProcessor(connectionString));

            return services;
        }
    }
}
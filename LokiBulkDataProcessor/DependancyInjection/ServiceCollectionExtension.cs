﻿using Loki.BulkDataProcessor.Commands;
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
            services.AddScoped<ISqlCommand, SqlServerCommand>();
            services.AddSingleton<IAppContext, AppContext>(x => new AppContext(connectionString, new ModelMappings(mappingAssembly), new DataTableMappings(mappingAssembly)));
            services.AddTransient<ISqlDbConnection, SqlDbConnection>();
            services.AddScoped<IBulkModelsCommand, BulkCopyModelsCommand>();
            services.AddScoped<IBulkDataTableCommand, BulkCopyDataTableCommand>();
            services.AddScoped<IBulkDataTableCommand, BulkUpdateDataTableCommand>();
            services.AddTransient<IDbConnection, SqlDbConnection>();
            services.AddSingleton<ICommandFactory, CommandFactory>();
            services.AddScoped<IBulkProcessor, BulkProcessor>();
            return services;
        }
    }
}
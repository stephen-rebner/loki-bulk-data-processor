using System;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer;
using Loki.BulkDataProcessor.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection;

/// <summary>Adds the SQL Server provider to a Loki registration.</summary>
public static class SqlServerBuilderExtensions
{
    /// <summary>Registers the SQL Server provider (SqlBulkCopy, Microsoft.Data.SqlClient) under <paramref name="name"/>.</summary>
    /// <param name="builder">The builder returned by <c>AddLokiBulkDataProcessor</c>.</param>
    /// <param name="name">Registered provider name; override it to register more than one SQL Server configuration.</param>
    public static ILokiBulkDataProcessorBuilder AddSqlServer(this ILokiBulkDataProcessorBuilder builder, string name = SqlServerProvider.Name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        builder.Services.AddSingleton<IBulkCopyProvider>(new SqlServerBulkCopyProvider(name));
        return builder;
    }
}

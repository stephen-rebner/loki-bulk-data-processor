using System;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection;

/// <summary>Adds the Oracle provider to a Loki registration.</summary>
public static class OracleBuilderExtensions
{
    /// <summary>Registers the Oracle provider (Oracle.ManagedDataAccess.Core) under <paramref name="name"/>.</summary>
    /// <param name="builder">The builder returned by <c>AddLokiBulkDataProcessor</c>.</param>
    /// <param name="configure">Optional settings (write mode, identifier resolution, boolean binding); the defaults suit most schemas.</param>
    /// <param name="name">Registered provider name; override it to register more than one Oracle configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option holds an undefined enum value.</exception>
    public static ILokiBulkDataProcessorBuilder AddOracle(this ILokiBulkDataProcessorBuilder builder,
        Action<OracleProviderOptions> configure = null, string name = OracleProvider.Name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OracleProviderOptions options = new();
        configure?.Invoke(options);
        builder.Services.AddSingleton<IBulkCopyProvider>(new OracleBulkCopyProvider(name, options.Validated()));
        return builder;
    }
}

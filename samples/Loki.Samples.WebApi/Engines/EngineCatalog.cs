using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.SqlServer;
using Loki.Samples.WebApi.Errors;

namespace Loki.Samples.WebApi.Engines;

/// <summary>
/// Per-request engine choice through <see cref="IBulkProcessorFactory"/> (the README's "Choosing the database at run time"):
/// the route value picks one of the <see cref="DatabaseEngine"/>s this code knows, the provider name and the connection string
/// come from code and configuration. Nothing the caller sends becomes a provider name or a connection string.
/// </summary>
internal sealed class EngineCatalog(IBulkProcessorFactory factory, IConfiguration configuration)
{
    private static readonly Dictionary<string, DatabaseEngine> EnginesByRouteValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sqlserver"] = DatabaseEngine.SqlServer,
        ["oracle"] = DatabaseEngine.Oracle
    };

    /// <summary>The route values, for error messages and the engine list.</summary>
    public static IReadOnlyCollection<string> RouteValues => EnginesByRouteValue.Keys;

    /// <summary>The engine for a route value; 404 for anything else (the value is not echoed back).</summary>
    public static DatabaseEngine Parse(string routeValue) =>
        routeValue is not null && EnginesByRouteValue.TryGetValue(routeValue, out DatabaseEngine engine)
            ? engine
            : throw RequestRejectedException.NotFound($"Unknown engine. Use one of: {string.Join(", ", RouteValues)}.");

    /// <summary>The Loki provider registered for <paramref name="engine"/> (array bind for Oracle).</summary>
    public static string ProviderNameOf(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.SqlServer => SqlServerProvider.Name,
        DatabaseEngine.Oracle => OracleProvider.Name,
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Not a known engine.")
    };

    /// <summary>A new processor (one per unit of work: processors are not thread-safe) on the engine's default provider.</summary>
    public IBulkProcessor CreateProcessor(DatabaseEngine engine) => Create(ProviderNameOf(engine), engine);

    /// <summary>A new processor on the direct-path Oracle provider. Staging tables only.</summary>
    public IBulkProcessor CreateOracleDirectPathProcessor() => Create(SampleProviders.OracleDirectPath, DatabaseEngine.Oracle);

    /// <summary>Whether <paramref name="engine"/> has a registered provider and a connection string.</summary>
    public bool IsConfigured(DatabaseEngine engine) =>
        factory.HasProvider(ProviderNameOf(engine)) && !string.IsNullOrWhiteSpace(ReadConnectionString(engine));

    /// <summary>The engine's connection string from configuration (<c>ConnectionStrings:SqlServer</c> / <c>:Oracle</c>); 503 if blank.</summary>
    public string ConnectionStringFor(DatabaseEngine engine)
    {
        string? connectionString = ReadConnectionString(engine);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw RequestRejectedException.Unavailable($"The {engine} engine has no connection string configured.")
            : connectionString;
    }

    private IBulkProcessor Create(string providerName, DatabaseEngine engine)
    {
        if (!factory.HasProvider(providerName))
        {
            throw RequestRejectedException.Unavailable($"No Loki provider named '{providerName}' is registered.");
        }

        return factory.Create(providerName, ConnectionStringFor(engine));
    }

    private string? ReadConnectionString(DatabaseEngine engine) => configuration.GetConnectionString(engine.ToString());
}

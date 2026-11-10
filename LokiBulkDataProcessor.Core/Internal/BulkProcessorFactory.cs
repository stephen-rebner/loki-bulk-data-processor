using System;
using System.Collections.Generic;
using System.Linq;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.Utils.Validation;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>Singleton. Immutable after construction; every <see cref="Create"/> returns a new processor.</summary>
internal sealed class BulkProcessorFactory : IBulkProcessorFactory
{
    private readonly IReadOnlyDictionary<string, IBulkCopyProvider> _providers;
    private readonly MappingRegistry _mappings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly JsonPayloadPolicy _jsonPolicy;

    public BulkProcessorFactory(IEnumerable<IBulkCopyProvider> providers, MappingRegistry mappings, LokiLogging logging,
        JsonPayloadPolicy jsonPolicy = null)
    {
        _providers = IndexByName(providers);
        _mappings = mappings;
        _loggerFactory = logging.LoggerFactory;
        _jsonPolicy = jsonPolicy ?? JsonPayloadPolicy.Default;
    }

    public IReadOnlyCollection<string> ProviderNames => _providers.Keys.ToArray();

    public bool HasProvider(string providerName) => providerName is not null && _providers.ContainsKey(providerName);

    public IBulkProcessor Create(string providerName, string connectionString)
    {
        providerName.ThrowIfNullOrEmptyString(nameof(providerName));
        connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
        return new BulkProcessor(FindProvider(providerName), connectionString, _mappings, _loggerFactory, _jsonPolicy);
    }

    /// <summary>The message names only the requested provider (sanitised); callers can list the others with <see cref="ProviderNames"/>.</summary>
    private IBulkCopyProvider FindProvider(string providerName) =>
        _providers.TryGetValue(providerName, out IBulkCopyProvider provider)
            ? provider
            : throw new ArgumentException(
                $"No bulk copy provider named '{MessageText.Sanitize(providerName)}' is registered. Use IBulkProcessorFactory.ProviderNames to list the registered providers.",
                nameof(providerName));

    private static Dictionary<string, IBulkCopyProvider> IndexByName(IEnumerable<IBulkCopyProvider> providers)
    {
        Dictionary<string, IBulkCopyProvider> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (IBulkCopyProvider provider in providers)
        {
            if (!byName.TryAdd(provider.Name, provider))
            {
                throw new InvalidOperationException($"Bulk copy provider '{provider.Name}' is registered more than once.");
            }
        }

        return byName;
    }
}

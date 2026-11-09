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

    public BulkProcessorFactory(IEnumerable<IBulkCopyProvider> providers, MappingRegistry mappings, LokiLogging logging)
    {
        _providers = IndexByName(providers);
        _mappings = mappings;
        _loggerFactory = logging.LoggerFactory;
    }

    public IReadOnlyCollection<string> ProviderNames => _providers.Keys.ToArray();

    public bool HasProvider(string providerName) => providerName is not null && _providers.ContainsKey(providerName);

    public IBulkProcessor Create(string providerName, string connectionString)
    {
        providerName.ThrowIfNullOrEmptyString(nameof(providerName));
        connectionString.ThrowIfNullOrEmptyString(nameof(connectionString));
        if (!_providers.TryGetValue(providerName, out IBulkCopyProvider provider))
        {
            throw new ArgumentException(
                $"No bulk copy provider named '{MessageText.Sanitize(providerName)}' is registered. Registered: {string.Join(", ", _providers.Keys)}.",
                nameof(providerName));
        }

        return new BulkProcessor(provider, connectionString, _mappings, _loggerFactory);
    }

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

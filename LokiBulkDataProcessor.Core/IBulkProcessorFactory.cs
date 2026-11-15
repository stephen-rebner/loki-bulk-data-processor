using System;
using System.Collections.Generic;

namespace Loki.BulkDataProcessor;

/// <summary>
/// Creates independent <see cref="IBulkProcessor"/> instances for any registered provider.
/// Registered as a singleton by <c>AddLokiBulkDataProcessor</c>; safe to inject anywhere and easy to mock.
/// </summary>
public interface IBulkProcessorFactory
{
    /// <summary>Names of all registered providers, for example "SqlServer".</summary>
    IReadOnlyCollection<string> ProviderNames { get; }

    /// <summary>True when a provider with this name (case-insensitive) is registered.</summary>
    bool HasProvider(string providerName);

    /// <summary>Creates a new processor. Each call returns a new instance with its own settings.</summary>
    /// <exception cref="ArgumentException">Unknown provider, or empty name or connection string.</exception>
    IBulkProcessor Create(string providerName, string connectionString);
}

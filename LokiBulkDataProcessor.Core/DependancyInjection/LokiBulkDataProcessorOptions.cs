using System;
using System.Collections.Generic;
using System.Reflection;
using Loki.BulkDataProcessor.Logging;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.DependancyInjection;

/// <summary>Options for <c>AddLokiBulkDataProcessor(options =&gt; ...)</c>. Read once, at registration.</summary>
public sealed class LokiBulkDataProcessorOptions
{
    internal List<Assembly> MappingAssemblies { get; } = new();

    internal Action<LokiLoggingOptions> ConsoleLogging { get; private set; }

    /// <summary>Explicit logger factory used by Loki only (highest precedence). Loki does not register or dispose it.</summary>
    public ILoggerFactory LoggerFactory { get; set; }

    /// <summary>Use the host's <see cref="ILoggerFactory"/> from DI. Off by default (logging is opt-in).</summary>
    public bool UseHostLoggerFactory { get; set; }

    /// <summary>Adds every concrete <c>ModelMapping&lt;T&gt;</c> and <c>DataMapping</c> in <paramref name="assembly"/>.
    /// Two mappings for the same model type or SourceTableName fail at registration.</summary>
    public LokiBulkDataProcessorOptions AddMappingsFrom(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        MappingAssemblies.Add(assembly);
        return this;
    }

    /// <summary>Loki-private console logging; it does not change the host's logging.</summary>
    public LokiBulkDataProcessorOptions UseConsoleLogging(Action<LokiLoggingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ConsoleLogging = configure;
        return this;
    }
}

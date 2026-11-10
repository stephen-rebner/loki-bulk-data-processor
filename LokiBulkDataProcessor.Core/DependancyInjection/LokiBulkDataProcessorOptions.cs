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

    /// <summary>
    /// When true, every JSON save (<c>IBulkProcessor.SaveAsync(Stream, ...)</c>) must find a registered <c>DataMapping</c>
    /// for its mapping key; otherwise it throws <see cref="InvalidOperationException"/> before any connection is opened.
    /// This stops a payload from writing arbitrary same-named columns when no mapping was intended. Default false.
    /// </summary>
    public bool RequireDataMappingForJson { get; set; }

    /// <summary>
    /// The largest JSON payload, in bytes, that <c>IBulkProcessor.SaveAsync(Stream, ...)</c> reads. A larger payload throws
    /// <see cref="System.IO.InvalidDataException"/> as soon as the limit is passed, before the whole payload is buffered
    /// (a seekable stream whose remaining length is over the limit is rejected before reading). Default <c>null</c>:
    /// no limit, so existing large imports keep working. Set it when payloads come from untrusted callers, because a
    /// JSON payload is buffered in memory in full before it is written.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public long? MaxJsonPayloadBytes
    {
        get => _maxJsonPayloadBytes;
        set
        {
            if (value is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxJsonPayloadBytes), value, "MaxJsonPayloadBytes must be greater than 0, or null for no limit.");
            }

            _maxJsonPayloadBytes = value;
        }
    }

    private long? _maxJsonPayloadBytes;

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

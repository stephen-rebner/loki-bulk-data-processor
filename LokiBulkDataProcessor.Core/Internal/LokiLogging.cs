using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>
/// Holds Loki's own <see cref="ILoggerFactory"/> (singleton). It is never registered as the app's ILoggerFactory.
/// Disposes the factory only when Loki created it (console logging).
/// </summary>
internal sealed class LokiLogging : IDisposable
{
    private readonly bool _ownsLoggerFactory;

    public LokiLogging(ILoggerFactory loggerFactory, bool ownsLoggerFactory = false)
    {
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _ownsLoggerFactory = ownsLoggerFactory && loggerFactory is not null;
    }

    public static LokiLogging None { get; } = new(NullLoggerFactory.Instance);

    public ILoggerFactory LoggerFactory { get; }

    public void Dispose()
    {
        if (_ownsLoggerFactory)
        {
            LoggerFactory.Dispose();
        }
    }
}

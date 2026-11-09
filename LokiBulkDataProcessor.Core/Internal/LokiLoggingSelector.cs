using System;
using Loki.BulkDataProcessor.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>
/// Picks Loki's logger factory at registration time. Precedence: an explicit factory, then Loki-private console
/// logging, then the host's ILoggerFactory (only when opted in), then a no-op factory.
/// </summary>
internal static class LokiLoggingSelector
{
    public static LokiLogging Select(ILoggerFactory explicitFactory, LokiLoggingOptions console, bool useHostLoggerFactory, IServiceProvider services)
    {
        if (explicitFactory is not null)
        {
            return new LokiLogging(explicitFactory);
        }

        if (console is { EnableConsoleLogging: true })
        {
            return new LokiLogging(CreateConsoleLoggerFactory(console), ownsLoggerFactory: true);
        }

        return useHostLoggerFactory
            ? new LokiLogging(services.GetService<ILoggerFactory>())
            : LokiLogging.None;
    }

    private static ILoggerFactory CreateConsoleLoggerFactory(LokiLoggingOptions console) =>
        LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(console.MinimumLogLevel));
}

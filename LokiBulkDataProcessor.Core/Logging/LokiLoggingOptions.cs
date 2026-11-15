using System;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Logging
{
    /// <summary>Loki-private console logging settings.</summary>
    [Obsolete(ObsoleteMessage)]
    public class LokiLoggingOptions
    {
        internal const string ObsoleteMessage =
            "Loki now logs through the host's ILoggerFactory by default. Configure console output in your host's logging, " +
            "or set LokiBulkDataProcessorOptions.LoggerFactory. Loki-private console logging will be removed in 10.0.";

        public bool EnableConsoleLogging { get; set; } = false;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }
}

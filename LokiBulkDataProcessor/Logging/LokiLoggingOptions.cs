using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Logging
{
    public class LokiLoggingOptions
    {
        public bool EnableConsoleLogging { get; set; } = false;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }
}
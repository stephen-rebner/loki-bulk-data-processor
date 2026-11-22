using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.TestLogging;

/// <summary>Captures every entry written through the loggers it creates. Pass it as <c>LokiBulkDataProcessorOptions.LoggerFactory</c>.</summary>
public sealed class TestLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(_entries);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    public bool Contains(LogLevel level, string fragment) =>
        Entries.Any(entry => entry.LogLevel == level && entry.Message.Contains(fragment, StringComparison.Ordinal));

    /// <summary>True when any entry's message or exception text contains <paramref name="text"/>.</summary>
    public bool AnyEntryContains(string text) =>
        Entries.Any(entry => entry.Message.Contains(text, StringComparison.Ordinal)
            || (entry.Exception != null && entry.Exception.ToString().Contains(text, StringComparison.Ordinal)));

    public sealed record LogEntry(LogLevel LogLevel, string Message, Exception Exception);

    private sealed class TestLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

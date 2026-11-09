using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>Captures every log entry written through the loggers it creates.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public bool IsDisposed { get; private set; }

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() => IsDisposed = true;

    public bool Contains(LogLevel level, string messageFragment) =>
        Entries.Any(entry => entry.Level == level && entry.Message.Contains(messageFragment, StringComparison.Ordinal));

    internal sealed record LogEntry(string Category, LogLevel Level, string Message, Exception Exception);

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}

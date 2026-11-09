using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class BulkProcessorLoggingTests : BaseIntegrationTest
    {
        private TestLoggerFactory _loggerFactory;
        private ServiceProvider _loggingServiceProvider;
        private IBulkProcessor _bulkProcessorWithTestLogger;

        [SetUp]
        public void CreateProcessorWithTestLogger()
        {
            _loggerFactory = new TestLoggerFactory();
            var services = new ServiceCollection();
            services.AddLokiBulkDataProcessor(GetConnectionString(), Assembly.GetExecutingAssembly(), _loggerFactory);
            _loggingServiceProvider = services.BuildServiceProvider();
            _bulkProcessorWithTestLogger = _loggingServiceProvider.CreateScope().ServiceProvider.GetRequiredService<IBulkProcessor>();
        }

        [TearDown]
        public async Task DisposeLoggingServiceProvider()
        {
            await _loggingServiceProvider.DisposeAsync();
        }

        [Test]
        public async Task BulkProcessor_ShouldLogInformation_WhenSavingData()
        {
            var models = new List<TestDbModel>
            {
                TestObjectFactory.TestDbModelObject()
                    .WithId(1)
                    .WithStringColumnValue("String Value 1")
                    .WithDateColumnValue(new DateTime(2020, 01, 26))
                    .WithBoolColumnValue(true)
                    .Build()
            };

            await _bulkProcessorWithTestLogger.SaveAsync(models, nameof(TestDbContext.TestDbModels));

            _loggerFactory.Contains(LogLevel.Information, "Starting bulk copy to TestDbModels via SqlServer").Should().BeTrue();
            _loggerFactory.Contains(LogLevel.Information, "Completed bulk copy to TestDbModels via SqlServer").Should().BeTrue();
        }

        [Test]
        public void BulkProcessor_ShouldLogPropertyChanges_WhenPropertiesAreModified()
        {
            _bulkProcessorWithTestLogger.BatchSize = 1000;
            _bulkProcessorWithTestLogger.Timeout = 120;

            _loggerFactory.Contains(LogLevel.Information, "Setting BatchSize").Should().BeTrue();
            _loggerFactory.Contains(LogLevel.Information, "Setting Timeout").Should().BeTrue();
        }

        [Test]
        public void BulkProcessor_ShouldLogError_WhenExceptionOccurs()
        {
            var models = new List<TestDbModel> { new() { Id = 1 } };
            const string nonExistentTable = "NonExistentTable";

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _bulkProcessorWithTestLogger.SaveAsync(models, nonExistentTable));

            _loggerFactory.Entries.Should().Contain(entry =>
                entry.LogLevel == LogLevel.Error &&
                entry.Message.Contains("Bulk copy to NonExistentTable via SqlServer failed with System.InvalidOperationException") &&
                entry.Exception == null);
        }

        [Test]
        public async Task BulkProcessor_ShouldKeepTheRowValueOutOfTheMessageAndLogs_WhenSqlServerRejectsTheValue()
        {
            const string rowValue = "secret-row-value";
            using var table = new System.Data.DataTable();
            table.Columns.Add("DateColumn", typeof(string));
            table.Rows.Add(rowValue);

            Func<Task> action = () => _bulkProcessorWithTestLogger.SaveAsync(table, nameof(TestDbContext.TestDbModels));

            var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
            thrown.Which.Message.Should().NotContain(rowValue);
            thrown.Which.InnerException.Should().NotBeNull();
            thrown.Which.ToString().Should().Contain(rowValue, "the driver's error, with the value, is kept on InnerException (#15)");
            _loggerFactory.Entries.Should().NotContain(entry =>
                entry.Message.Contains(rowValue) || (entry.Exception != null && entry.Exception.ToString().Contains(rowValue)));
        }

        [Test]
        public async Task BulkProcessor_ShouldNeverLogTheConnectionString()
        {
            var models = new List<TestDbModel> { new() { Id = 1, StringColumn = "x" } };
            await _bulkProcessorWithTestLogger.SaveAsync(models, nameof(TestDbContext.TestDbModels));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _bulkProcessorWithTestLogger.SaveAsync(models, "NonExistentTable"));

            _loggerFactory.Entries.Should().NotContain(entry =>
                entry.Message.Contains(TestPassword) || (entry.Exception != null && entry.Exception.ToString().Contains(TestPassword)));
        }
    }

    /// <summary>Captures every entry written through the loggers it creates.</summary>
    public sealed class TestLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new TestLogger(_entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        public bool Contains(LogLevel level, string fragment) =>
            Entries.Any(entry => entry.LogLevel == level && entry.Message.Contains(fragment, StringComparison.Ordinal));

        public sealed record LogEntry(LogLevel LogLevel, string Message, Exception Exception);

        private sealed class TestLogger(ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
                entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}

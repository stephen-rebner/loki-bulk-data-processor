using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Core.Context.Interfaces;
using Loki.BulkDataProcessor.Commands.Factory;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using LokiBulkDataProcessor.Core.Interfaces;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class BulkProcessorLoggingTests : BaseIntegrationTest
    {
        private TestLogger<BulkProcessor> _testLogger;
        private IBulkProcessor _bulkProcessorWithTestLogger;

        [SetUp]
        public async Task TestSetup()
        {
            await base.TestSetup();
            
            // Create a test logger and reconfigure the BulkProcessor to use it
            _testLogger = new TestLogger<BulkProcessor>();
            var commandFactory = ServiceProvider.GetService<ICommandFactory>();
            var appContext = ServiceProvider.GetService<IAppContext>();
            _bulkProcessorWithTestLogger = new BulkProcessor(commandFactory, appContext, _testLogger);
            _bulkProcessorWithTestLogger.ConnectionString = GetConnectionString();
        }

        [Test]
        public async Task BulkProcessor_ShouldLogInformation_WhenSavingData()
        {
            // Arrange
            var models = new List<TestDbModel>
            {
                TestObjectFactory.TestDbModelObject()
                    .WithId(1)
                    .WithStringColumnValue("String Value 1")
                    .WithDateColumnValue(new DateTime(2020, 01, 26))
                    .WithBoolColumnValue(true)
                    .Build()
            };
            
            // Act
            await _bulkProcessorWithTestLogger.SaveAsync(models, nameof(TestDbContext.TestDbModels));
            
            // Assert
            _testLogger.LogEntries.Should().Contain(entry => 
                entry.LogLevel == LogLevel.Information && 
                entry.Message.Contains("Starting bulk copy of 1 models to table TestDbModels"));
            
            _testLogger.LogEntries.Should().Contain(entry => 
                entry.LogLevel == LogLevel.Information && 
                entry.Message.Contains("Successfully copied 1 models to table TestDbModels"));
        }
        
        [Test]
        public void BulkProcessor_ShouldLogPropertyChanges_WhenPropertiesAreModified()
        {
            // Act
            _bulkProcessorWithTestLogger.BatchSize = 1000;
            _bulkProcessorWithTestLogger.Timeout = 120;
            
            // Assert
            _testLogger.LogEntries.Should().Contain(entry => 
                entry.LogLevel == LogLevel.Information && 
                entry.Message.Contains("Setting BatchSize"));
            
            _testLogger.LogEntries.Should().Contain(entry => 
                entry.LogLevel == LogLevel.Information && 
                entry.Message.Contains("Setting Timeout"));
        }
        
        [Test]
        public void BulkProcessor_ShouldLogError_WhenExceptionOccurs()
        {
            // Arrange - create invalid scenario by using non-existent table
            var models = new List<TestDbModel> { new() { Id = 1 } };
            const string nonExistentTable = "NonExistentTable";
            
            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _bulkProcessorWithTestLogger.SaveAsync(models, nonExistentTable));
            
            _testLogger.LogEntries.Should().Contain(entry => 
                entry.LogLevel == LogLevel.Error && 
                entry.Message.Contains($"Error during bulk copy"));
           
        }
    }
    
    // Simple test logger implementation that captures log entries
    public class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];
        
        public IDisposable BeginScope<TState>(TState state) => default;
        
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                Message = message,
                Exception = exception
            });
        }
        
        public class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public EventId EventId { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
        }
    }
}
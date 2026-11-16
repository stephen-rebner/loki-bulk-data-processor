using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestLogging;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestObjectBuilders;
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
            services.AddLokiBulkDataProcessor(GetConnectionString(), typeof(PostMapping).Assembly, _loggerFactory);
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
            var models = new List<TestDbModel> { new() { StringColumn = "x" } };
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
    }
}

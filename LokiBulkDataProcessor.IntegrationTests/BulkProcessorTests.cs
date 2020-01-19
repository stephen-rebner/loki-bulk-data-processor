using NUnit.Framework;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModel;
using System;
using System.Collections.Generic;
using Loki.BulkDataProcessor;
using System.Threading.Tasks;
using System.Linq;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class BulkProcessorTests : BaseIntegrationTest
    {
        private BulkProcessor _bulkProcessor;

        [SetUp]
        public void Setup()
        {
            _bulkProcessor = new BulkProcessor("Server=(local);Database=IntegrationTestsDb;Trusted_Connection=True;MultipleActiveResultSets=true");
        }

        [Test]
        public async Task SaveAsync_SavesDataSuccessfully()
        {
            var model = new TestDbModel
            {
                Id = 1,
                StringColumn = "Test String",
                BoolColumn = false,
                DateColumn = DateTime.Now
            };

            var models = new List<TestDbModel> { model };

            await _bulkProcessor.SaveAsync(models, "TestDbModels");

            var results = TestDbContext.TestDbModels.ToList();

            results.Should().BeEquivalentTo(models);
        }
    }
}

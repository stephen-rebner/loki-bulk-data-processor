using NUnit.Framework;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Loki.BulkDataProcessor.Commands.Factory;
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
            _bulkProcessor = new BulkProcessor("Server=(local);Database=IntegrationTestsDb;Trusted_Connection=True;MultipleActiveResultSets=true", 
                new CommandFactory());
        }

        [Test]
        public async Task SaveAsync_ShouldSaveSuccessfully_WhenPropsDiffOrderFromDbColumnNames()
        {
            var model1 = TestObjectFactory.TestDbModelObject()
                .WithId(1)
                .WithStringColumnValue("String Value 1")
                .WithDateColumnValue(new System.DateTime(2020, 01, 26))
                .WithBoolColumnValue(true)
                .WithNullableBoolColumnValue(null)
                .WithNullableDateColumnValue(null)
                .Build();

            var model2 = TestObjectFactory.TestDbModelObject()
                .WithId(2)
                .WithStringColumnValue("String Value 2")
                .WithDateColumnValue(new System.DateTime(2020, 01, 27))
                .WithBoolColumnValue(false)
                .WithNullableBoolColumnValue(true)
                .WithNullableDateColumnValue(new System.DateTime(2020, 01, 19))
                .Build(); 
            
            var model3 = TestObjectFactory.TestDbModelObject()
                 .WithId(3)
                 .WithStringColumnValue("String Value 3")
                 .WithDateColumnValue(new System.DateTime(2020, 01, 28))
                 .WithBoolColumnValue(true)
                 .WithNullableBoolColumnValue(false)
                 .WithNullableDateColumnValue(new System.DateTime(2020, 1, 10))
                 .Build();

            var models = new List<TestDbModel> { model1, model2, model3 };

            await _bulkProcessor.SaveAsync(models, nameof(TestDbContext.TestDbModels));

            var results = TestDbContext.TestDbModels.OrderBy(x => x.Id).ToList();

            results.Should().BeEquivalentTo(models);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenColsSameOrderAsDbColumns()
        {
            using var datatable = TestObjectFactory.NewTestDataTable()
                .WithRowData(1, "String Value 1", true, new System.DateTime(2020, 01, 26), null, null)
                .WithRowData(2, "String Value 2", false, new System.DateTime(2020, 01, 27), true, new System.DateTime(2020, 01, 19))
                .Build();

            var exptectedModel1 = TestObjectFactory.TestDbModelObject()
                .WithId(1)
                .WithStringColumnValue("String Value 1")
                .WithDateColumnValue(new System.DateTime(2020, 01, 26))
                .WithBoolColumnValue(true)
                .WithNullableBoolColumnValue(null)
                .WithNullableDateColumnValue(null)
                .Build();

            var expectedModel2 = TestObjectFactory.TestDbModelObject()
                .WithId(2)
                .WithStringColumnValue("String Value 2")
                .WithDateColumnValue(new System.DateTime(2020, 01, 27))
                .WithBoolColumnValue(false)
                .WithNullableBoolColumnValue(true)
                .WithNullableDateColumnValue(new System.DateTime(2020, 01, 19))
                .Build();

            var expctedResults = new List<TestDbModel> { exptectedModel1, expectedModel2 };

            await _bulkProcessor.SaveAsync(datatable, nameof(TestDbContext.TestDbModels));

            var results = TestDbContext.TestDbModels.OrderBy(x => x.Id).ToList();

            results.Should().BeEquivalentTo(expctedResults);
        }
    }
}

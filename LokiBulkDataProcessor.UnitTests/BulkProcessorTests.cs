using FluentAssertions;
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.UnitTests.TestModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.UnitTests
{
    public class BulkProcessorTests
    {
        private const string TestConnectionStringValue = "A dummy connection string";
        private const string TestDestinationTableName = "A dummy table name";
        private readonly IEnumerable<ValidModelObject> ModelObjects = new List<ValidModelObject> { new ValidModelObject() };
        private IBulkProcessor _bulkProcessor;

        [SetUp]
        public void SetUp()
        {
            _bulkProcessor = new BulkProcessor(TestConnectionStringValue);
        }

        [Test]
        public void Timeout_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => _bulkProcessor.Timeout = -1;

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The Timeout value must be greater than or equal to 0 (Parameter 'Timeout')");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Timeout_ShouldNotThrow_WhenValueIsGtOrEqToZero(int timeoutValue)
        {
            Action action = () => _bulkProcessor.Timeout = timeoutValue;

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void BatchSize_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => _bulkProcessor.BatchSize = -1;

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The BatchSize value must be greater than or equal to 0 (Parameter 'BatchSize')");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BatchSize_ShouldNotThrow_WhenValueIsGtOrEqToZero(int batchSizeValue)
        {
            Action action = () => _bulkProcessor.BatchSize = batchSizeValue;

            action.Should().NotThrow<ArgumentException>();
        }

        [TestCase("")]
        [TestCase(null)]
        public void DestinationTable_ShouldThrow_IfValueIsNullOrEmpty(string destinationTableValue)
        {
            Action action = () => _bulkProcessor.DestinationTableName = destinationTableValue;

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("DestinationTableName must not be null or empty. (Parameter 'DestinationTableName')");
        }

        [Test]
        public void BatchSize_ShouldNotThrow_WhenValueIsNotNullOrEmpty()
        {
            Action action = () => _bulkProcessor.DestinationTableName = TestDestinationTableName;

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsNull()
        {
            _bulkProcessor.DestinationTableName = TestDestinationTableName;
            IEnumerable<ValidModelObject> nullModel = null;
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(nullModel);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsEmpty()
        {
            _bulkProcessor.DestinationTableName = TestDestinationTableName;
            var emptyModel = Enumerable.Empty<ValidModelObject>();
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(emptyModel);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDestinationTableNameIsNotSet()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("DestinationTableName must not be null or empty. (Parameter 'DestinationTableName')");
        }
    }
}

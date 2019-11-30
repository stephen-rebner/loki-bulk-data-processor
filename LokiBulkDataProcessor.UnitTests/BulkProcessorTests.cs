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
        private const string DummyConnectionStringValue = "A dummy connection string";
        private const string DummyDestinationTableName = "A dummy table name";
        private readonly IEnumerable<ValidModelObject> ModelObjects = new List<ValidModelObject> { new ValidModelObject() };
        private IBulkProcessor _bulkProcessor;

        [SetUp]
        public void SetUp()
        {
            _bulkProcessor = new BulkProcessor();
        }

        [Test]
        public void Timeout_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => _bulkProcessor.Timeout = -1;

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The Timeout value must be greater than or equal to 0 (Parameter 'Timeout')");
        }

        [Test]
        public void BatchSize_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => _bulkProcessor.BatchSize = -1;

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The BatchSize value must be greater than or equal to 0 (Parameter 'BatchSize')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsNull()
        {
            IEnumerable<ValidModelObject> nullModel = null;
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(nullModel, DummyConnectionStringValue, DummyDestinationTableName);

            action.Should()
              .Throw<ArgumentNullException>()
              .WithMessage("The data collection to be proccessed is null. Please supply some data. (Parameter 'dataToProcess')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsEmpty()
        {
            var emptyModel = Enumerable.Empty<ValidModelObject>();
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(emptyModel, DummyConnectionStringValue, DummyDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data collection to be processed is empty. Please Supply some data. (Parameter 'dataToProcess')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfConnectionStringIsNull()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects, null, DummyDestinationTableName);

            action.Should()
              .Throw<ArgumentNullException>()
              .WithMessage("The connection string must not be null. (Parameter 'connectionString')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfConnectionStringIsEmpty()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects, string.Empty, DummyDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The connection string must not be empty. (Parameter 'connectionString')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDestinationTableNameIsNull()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects, DummyConnectionStringValue, null);

            action.Should()
              .Throw<ArgumentNullException>()
              .WithMessage("The destination table name must not be null. (Parameter 'destinationTableName')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDestinationTableNameIsEmpty()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects, DummyConnectionStringValue, string.Empty);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The destination table name must not be empty. (Parameter 'destinationTableName')");
        }

    }
}

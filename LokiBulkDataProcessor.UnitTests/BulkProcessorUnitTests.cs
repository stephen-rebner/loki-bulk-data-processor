using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Commands.Factory;
using LokiBulkDataProcessor.UnitTests.TestModels;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.UnitTests
{
    public class BulkProcessorUnitTests
    {
        private const string TestConnectionStringValue = "Server=(local)";
        private const string TestDestinationTableName = "A dummy table name";
        private readonly IEnumerable<ValidModelObject> ModelObjects = new List<ValidModelObject> { new ValidModelObject() };

        private IBulkProcessor _bulkProcessor;
        private Mock<ICommandFactory> _commandFactory;

        private int _timeoutValue;
        private int _batchSize;

        [SetUp]
        public void SetUp()
        {
            _commandFactory = new Mock<ICommandFactory>();
            _bulkProcessor = new BulkProcessor(TestConnectionStringValue, _commandFactory.Object);
        }

        [Test]
        public void Timeout_ShouldThrow_IfValueSetLessThanZero()
        {
            GivenTimeoutValueOf(-1);

            Action action = () => WhenTimeoutPropertyIsUpdated();

            ActionShouldThrowArgExceptionWithMessage(
                action,
                "The Timeout value must be greater than or equal to 0 (Parameter 'Timeout')");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Timeout_ShouldNotThrow_WhenValueIsGtOrEqToZero(int timeoutValue)
        {
            GivenTimeoutValueOf(timeoutValue);

            Action action = () => WhenTimeoutPropertyIsUpdated();

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void BatchSize_ShouldThrow_IfValueSetLessThanZero()
        {
            GivenBatchSizeValueOf(-1);

            Action action = () => WhenBatchSizePropertyIsUpdated();

            ActionShouldThrowArgExceptionWithMessage(
                action,
                "The BatchSize value must be greater than or equal to 0 (Parameter 'BatchSize')");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BatchSize_ShouldNotThrow_WhenValueIsGtOrEqToZero(int batchSizeValue)
        {
            GivenBatchSizeValueOf(batchSizeValue);

            Action action = () => WhenBatchSizePropertyIsUpdated();

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsNull()
        {
            IEnumerable<ValidModelObject> nullModel = null;
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(nullModel, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsEmpty()
        {
            var emptyModel = Enumerable.Empty<ValidModelObject>();
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(emptyModel, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void SaveAsync_ShouldThrow_IfDestinationTableNameIsNotSet(string destinationTableName)
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(ModelObjects, destinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("DestinationTableName must not be null or empty. (Parameter 'destinationTableName')");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataTableIsNull()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(null, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data table provided is either null or contains no data");
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataTableHasNoRows()
        {
            using var dataTable = new DataTable();
            dataTable.Columns.Add(new DataColumn("Test Column"));

            Func<Task> action = async () => await _bulkProcessor.SaveAsync(dataTable, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data table provided is either null or contains no data");
        }


        #region Helper Test Methods

        private void GivenTimeoutValueOf(int timeoutValue)
        {
            _timeoutValue = timeoutValue;
        }

        private void WhenTimeoutPropertyIsUpdated()
        {
            _bulkProcessor.Timeout = _timeoutValue;
        }

        private void GivenBatchSizeValueOf(int batchSize)
        {
            _batchSize = batchSize;
        }

        private void WhenBatchSizePropertyIsUpdated()
        {
            _bulkProcessor.BatchSize = _batchSize;
        }

        private void ActionShouldThrowArgExceptionWithMessage(Action action, string errorMessage)
        {
            action.Should()
              .Throw<ArgumentException>()
              .WithMessage(errorMessage);
        }

        #endregion
    }
}

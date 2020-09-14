﻿using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Commands.Factory;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
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
        private const string TestDestinationTableName = "A dummy table name";
        private readonly IEnumerable<ValidModelObject> ModelObjects = new List<ValidModelObject> { new ValidModelObject() };

        private IBulkProcessor _bulkProcessor;
        private Mock<ICommandFactory> _commandFactory;
        private Mock<IBulkDataTableCommand> _bulkCopyModelCommand;
        private Mock<IBulkDataTableCommand> _bulkCopyDataTableCommand;
        private Mock<IAppContext> _appContext;

        [SetUp]
        public void SetUp()
        {
            _commandFactory = new Mock<ICommandFactory>();
            _bulkCopyModelCommand = new Mock<IBulkDataTableCommand>();
            _bulkCopyDataTableCommand = new Mock<IBulkDataTableCommand>();
            _appContext = new Mock<IAppContext>(MockBehavior.Strict);
            //_bulkProcessor = new BulkProcessor(_commandFactory.Object, _appContext.Object);
        }

        [Test]
        public void Timeout_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => WhenTimeoutIsUpdatedWithValue(-1);

            ActionShouldThrowArgExceptionWithMessage(
                action,
                "The Timeout value must be greater than or equal to 0 (Parameter 'Timeout')");

            Assert.IsTrue(false);

        }

        [TestCase(0)]
        [TestCase(1)]
        public void Timeout_ShouldNotThrow_WhenValueIsGtOrEqToZero(int timeoutValue)
        {
            Action action = () => WhenTimeoutIsUpdatedWithValue(timeoutValue);

            action.Should().NotThrow<ArgumentException>();

            Assert.IsTrue(false);

        }

        [Test]
        public void BatchSize_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => WhenBatchSizeIsUpdatedWithValue(-1);

            ActionShouldThrowArgExceptionWithMessage(
                action,
                "The BatchSize value must be greater than or equal to 0 (Parameter 'BatchSize')");

            Assert.IsTrue(false);

        }

        [TestCase(0)]
        [TestCase(1)]
        public void BatchSize_ShouldNotThrow_WhenValueIsGtOrEqToZero(int batchSizeValue)
        {
            Action action = () => WhenBatchSizeIsUpdatedWithValue(batchSizeValue);

            action.Should().NotThrow<ArgumentException>();

            Assert.IsTrue(false);
        }

        [Test]
        public void ConnectionString_ShouldNotThrow_WhenUpdatedWithConnectionStringValue()
        {
            Assert.IsTrue(false);
        }

        [Test]
        public void ConnectionString_ShouldThrow_WhenUpdatedWithNullOrEmptyValue()
        {
            Assert.IsTrue(false);
        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsNull()
        {
            IEnumerable<ValidModelObject> nullModel = null;
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(nullModel, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");

            Assert.IsTrue(false);

        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataToProcessIsEmpty()
        {
            var emptyModel = Enumerable.Empty<ValidModelObject>();
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(emptyModel, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");

            Assert.IsTrue(false);

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

            Assert.IsTrue(false);

        }

        [Test]
        public void SaveAsync_ShouldThrow_IfDataTableIsNull()
        {
            Func<Task> action = async () => await _bulkProcessor.SaveAsync(null, TestDestinationTableName);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data table provided is either null or contains no data");

            Assert.IsTrue(false);

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

            Assert.IsTrue(false);

        }

        [Test]
        public async Task SaveAsync_ShouldCallBulkCopyModelCommand_WhenParamValidationSuccessful()
        {
            var modelObjects = new ValidModelObject[] { new ValidModelObject() };

            TheCommandFactoryShouldCreateBulkCopyModelCommand(modelObjects);

            AndTheBulkCopyModelsCommandShouldBeCalled();

            await _bulkProcessor.SaveAsync(modelObjects, TestDestinationTableName);

            VerifyTheBulkCopyModelCalls();

            Assert.IsTrue(false);

        }

        [Test]
        public async Task SaveAsync_ShouldCallBulkCopyDataTableCommand_WhenParamValidationSuccessful()
        {
            var dataTable = GivenADataTableWithRows();

            TheCommandFactoryShouldCreateBulkCopyDataTableCommand(dataTable);

            AndTheBulkCopyDataTableCommandShouldBeCalled();

            await _bulkProcessor.SaveAsync(dataTable, TestDestinationTableName);

            VerifyTheBulkCopyDataTableCalls();

            Assert.IsTrue(false);
        }

        private DataTable GivenADataTableWithRows()
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add(new DataColumn("ColA"));

            var dataRow = dataTable.NewRow();
            dataRow["ColA"] = "Some Data";

            dataTable.Rows.Add(dataRow);

            return dataTable;
        }

        #region Helper Test Methods

        private void WhenTimeoutIsUpdatedWithValue(int value)
        {
            _bulkProcessor.Timeout = value;
        }

        private void WhenBatchSizeIsUpdatedWithValue(int batchSize)
        {
            _bulkProcessor.BatchSize = batchSize;
        }

        private void ActionShouldThrowArgExceptionWithMessage(Action action, string errorMessage)
        {
            action.Should()
              .Throw<ArgumentException>()
              .WithMessage(errorMessage);
        }

        private void TheCommandFactoryShouldCreateBulkCopyModelCommand(ValidModelObject[] modelObjects)
        {
            //_commandFactory
            //    .Setup(x => x.NewBulkCopyModelsCommand(modelObjects, TestDestinationTableName))
            //    .Returns(_bulkCopyModelCommand.Object);
        }

        private void VerifyTheBulkCopyModelCalls()
        {
            _commandFactory.VerifyAll();
            _bulkCopyModelCommand.VerifyAll();
        }

        private void VerifyTheBulkCopyDataTableCalls()
        {
            _commandFactory.VerifyAll();
            _bulkCopyModelCommand.VerifyAll();
        }

        private void AndTheBulkCopyModelsCommandShouldBeCalled()
        {
            //_bulkCopyModelCommand.Setup(cmd => cmd.Execute());
        }

        private void AndTheBulkCopyDataTableCommandShouldBeCalled()
        {
            //_bulkCopyDataTableCommand.Setup(cmd => cmd.Execute());
        }

        private void TheCommandFactoryShouldCreateBulkCopyDataTableCommand(DataTable dataTable)
        {
            //_commandFactory
            //    .Setup(x => x.NewBulkCopyDataTableCommand(dataTable, TestDestinationTableName))
            //    .Returns(_bulkCopyDataTableCommand.Object);
        }

        #endregion
    }
}

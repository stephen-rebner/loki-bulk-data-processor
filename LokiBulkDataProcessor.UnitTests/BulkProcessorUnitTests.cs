using FluentAssertions;
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
        private Mock<IBulkModelsCommand> _bulkCopyModelCommand;
        private Mock<IBulkDataTableCommand> _bulkCopyDataTableCommand;
        private Mock<IAppContext> _appContext;
        private Mock<IDbTransaction> _transaction;

        [SetUp]
        public void SetUp()
        {
            _commandFactory = new Mock<ICommandFactory>();
            _bulkCopyModelCommand = new Mock<IBulkModelsCommand>();
            _bulkCopyDataTableCommand = new Mock<IBulkDataTableCommand>();
            _appContext = new Mock<IAppContext>(MockBehavior.Strict);
            _transaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            _bulkProcessor = new BulkProcessor(_commandFactory.Object, _appContext.Object);
        }

        [Test]
        public void Timeout_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => WhenTimeoutIsUpdatedWithValue(-1);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The Timeout value must be greater than or equal to 0 (Parameter 'Timeout')");

        }

        [TestCase(0)]
        [TestCase(1)]
        public void Timeout_ShouldNotThrow_WhenValueIsGtOrEqToZero(int timeoutValue)
        {
            Action action = () => WhenTimeoutIsUpdatedWithValue(timeoutValue);

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void BatchSize_ShouldThrow_IfValueSetLessThanZero()
        {
            Action action = () => WhenBatchSizeIsUpdatedWithValue(-1);

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The BatchSize value must be greater than or equal to 0 (Parameter 'BatchSize')");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BatchSize_ShouldNotThrow_WhenValueIsGtOrEqToZero(int batchSizeValue)
        {
            Action action = () => WhenBatchSizeIsUpdatedWithValue(batchSizeValue);

            action.Should().NotThrow<ArgumentException>();
        }

        [Test]
        public void Transaction_ShouldThrow_IfValueIsNull()
        {
            Action action = () => WhenTheTransactionIsUpdatedWith(null);

            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'Transaction')");
        }

        [Test]
        public void Transaction_ShouldNotThrow_IfValueIsSqlTransaction()
        {
            Action action = () =>  WhenTheTransactionIsUpdatedWith(_transaction.Object);

            TheTransactionShouldBePassedToTheAppContext();

            action.Should().NotThrow();
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

        [Test]
        public async Task SaveAsync_ShouldCallBulkCopyModelCommand_WhenParamValidationSuccessful()
        {
            var modelObjects = new ValidModelObject[] { new ValidModelObject() };

            TheCommandFactoryShouldCreateBulkCopyModelCommand();

            AndTheBulkCopyModelsCommandShouldBeCalled(modelObjects);

            await _bulkProcessor.SaveAsync(modelObjects, TestDestinationTableName);

            VerifyTheBulkCopyModelCalls();
        }

        [Test]
        public async Task SaveAsync_ShouldCallBulkCopyDataTableCommand_WhenParamValidationSuccessful()
        {
            var dataTable = GivenADataTableWithRows();

            TheCommandFactoryShouldCreateBulkCopyDataTableCommand(dataTable);

            AndTheBulkCopyDataTableCommandShouldBeCalled(dataTable);

            await _bulkProcessor.SaveAsync(dataTable, TestDestinationTableName);

            VerifyTheBulkCopyDataTableCalls();
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

        private void WhenTheTransactionIsUpdatedWith(IDbTransaction transaction)
        {
            _bulkProcessor.Transaction = transaction;
        }

        private void TheTransactionShouldBePassedToTheAppContext()
        {
            _appContext.Setup(context => context.SetTransaction(_transaction.Object));
        }

        private void TheCommandFactoryShouldCreateBulkCopyModelCommand()
        {
            _commandFactory
                .Setup(x => x.NewBulkCopyModelsCommand())
                .Returns(_bulkCopyModelCommand.Object);
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

        private void AndTheBulkCopyModelsCommandShouldBeCalled<T>(IEnumerable<T> dataToProcess) where T : class
        {
            _bulkCopyModelCommand.Setup(cmd => cmd.Execute(dataToProcess, TestDestinationTableName));
        }

        private void AndTheBulkCopyDataTableCommandShouldBeCalled(DataTable dataToProcess)
        {
            _bulkCopyDataTableCommand.Setup(cmd => cmd.Execute(dataToProcess, TestDestinationTableName));
        }

        private void TheCommandFactoryShouldCreateBulkCopyDataTableCommand(DataTable dataTable)
        {
            _commandFactory
                .Setup(x => x.NewBulkCopyDataTableCommand())
                .Returns(_bulkCopyDataTableCommand.Object);
        }

        #endregion
    }
}

using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using LokiBulkDataProcessor.UnitTests.TestModels;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.UnitTests
{
    public class BulkProcessorUnitTests
    {
        private const string TestDestinationTableName = "A dummy table name";
        private const string ConnectionString = "Server=fake;Password=secret";
        private static readonly ValidModelObject[] ModelObjects = { new ValidModelObject() };

        private FakeProvider _provider;
        private RecordingLoggerFactory _loggerFactory;
        private IBulkProcessor _bulkProcessor;

        [SetUp]
        public void SetUp()
        {
            _provider = new FakeProvider("FakeDb");
            _loggerFactory = new RecordingLoggerFactory();
            _bulkProcessor = CreateProcessor(TestMappingRegistry.Valid());
        }

        [Test]
        public void ProviderName_ShouldBeTheProvidersName()
        {
            _bulkProcessor.ProviderName.Should().Be("FakeDb");
        }

        [Test]
        public void Settings_ShouldHaveTheDocumentedDefaults_WhenNotSet()
        {
            _bulkProcessor.Timeout.Should().Be(30);
            _bulkProcessor.BatchSize.Should().Be(5000);
            _bulkProcessor.Transaction.Should().BeNull();
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
            _bulkProcessor.Timeout = timeoutValue;

            _bulkProcessor.Timeout.Should().Be(timeoutValue);
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
            _bulkProcessor.BatchSize = batchSizeValue;

            _bulkProcessor.BatchSize.Should().Be(batchSizeValue);
        }

        [Test]
        public void Transaction_ShouldClearTheTransaction_WhenSetToNull()
        {
            GivenTheProcessorUsesAnExternalTransaction();

            _bulkProcessor.Transaction = null;

            _bulkProcessor.Transaction.Should().BeNull();
        }

        [Test]
        public void Transaction_ShouldBeKept_WhenTheProviderAcceptsIt()
        {
            FakeDbTransaction transaction = GivenTheProcessorUsesAnExternalTransaction();

            _bulkProcessor.Transaction.Should().BeSameAs(transaction);
        }

        [Test]
        public void Transaction_ShouldThrowArgumentExceptionAndNotBeKept_WhenTheProviderRejectsIt()
        {
            _provider.RejectExternal();

            Action action = () => _bulkProcessor.Transaction = new Mock<IDbTransaction>().Object;

            action.Should().Throw<ArgumentException>();
            _bulkProcessor.Transaction.Should().BeNull();
        }

        [Test]
        public void Settings_ShouldBeIndependent_BetweenTwoProcessors()
        {
            IBulkProcessor other = CreateProcessor(MappingRegistry.Empty);
            GivenTheProcessorUsesAnExternalTransaction();

            _bulkProcessor.BatchSize = 10;
            _bulkProcessor.Timeout = 99;

            other.BatchSize.Should().Be(5000);
            other.Timeout.Should().Be(30);
            other.Transaction.Should().BeNull();
        }

        [Test]
        public async Task SaveAsync_ShouldUseTheSettingsAtTheStartOfTheCall_WhenTheyChangeWhileTheCallRuns()
        {
            var openGate = new TaskCompletionSource();
            _provider.Connection.OpenGate = openGate.Task;
            _bulkProcessor.BatchSize = 100;
            _bulkProcessor.Timeout = 5;

            Task save = _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);
            _bulkProcessor.BatchSize = 999;
            _bulkProcessor.Timeout = 60;
            openGate.SetResult();
            await save;

            _provider.Contexts.Single().Options.Should().Be(new BulkWriteOptions(100, 5));
        }

        [Test]
        public async Task SaveAsync_ShouldWriteThroughTheExternalTransaction_WhenOneIsSet()
        {
            FakeDbTransaction transaction = GivenTheProcessorUsesAnExternalTransaction();

            await _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);

            _provider.Contexts.Single().Transaction.Should().BeSameAs(transaction);
            transaction.CommitCount.Should().Be(0);
            _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfDataToProcessIsNull()
        {
            IEnumerable<ValidModelObject> nullModel = null;
            Func<Task> action = () => _bulkProcessor.SaveAsync(nullModel, TestDestinationTableName);

            await action.Should()
              .ThrowAsync<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfDataToProcessIsEmpty()
        {
            Func<Task> action = () => _bulkProcessor.SaveAsync(Enumerable.Empty<ValidModelObject>(), TestDestinationTableName);

            await action.Should()
              .ThrowAsync<ArgumentException>()
              .WithMessage("The dataToProcess collection must not be null or empty. (Parameter 'dataToProcess')");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public async Task SaveAsync_ShouldThrow_IfDestinationTableNameIsNotSet(string destinationTableName)
        {
            await TheCallShouldThrowForTheDestination(() => _bulkProcessor.SaveAsync(ModelObjects, destinationTableName));
            await TheCallShouldThrowForTheDestination(() => _bulkProcessor.SaveAsync(GivenADataTableWithRows("ColA"), destinationTableName));
            await TheCallShouldThrowForTheDestination(() => _bulkProcessor.SaveAsync(new Mock<IDataReader>().Object, destinationTableName));
            _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfDataTableIsNull()
        {
            Func<Task> action = () => _bulkProcessor.SaveAsync(dataTable: null, TestDestinationTableName);

            await action.Should()
              .ThrowAsync<ArgumentException>()
              .WithMessage("The data table provided is either null or contains no data");
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfDataTableHasNoRows()
        {
            using var dataTable = new DataTable();
            dataTable.Columns.Add(new DataColumn("Test Column"));

            Func<Task> action = () => _bulkProcessor.SaveAsync(dataTable, TestDestinationTableName);

            await action.Should()
              .ThrowAsync<ArgumentException>()
              .WithMessage("The data table provided is either null or contains no data");
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfDataReaderIsNull()
        {
            Func<Task> action = () => _bulkProcessor.SaveAsync((IDataReader)null, TestDestinationTableName);

            await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("dataReader");
        }

        [Test]
        public async Task SaveAsync_ShouldThrow_IfJsonStreamIsNull()
        {
            Func<Task> action = () => _bulkProcessor.SaveAsync((Stream)null);

            await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("jsonStream");
        }

        [Test]
        public async Task SaveAsync_ShouldWriteModelsWithTheirModelMapping_WhenOneIsRegistered()
        {
            await _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);

            BulkWriteTarget target = _provider.Targets.Single();
            target.TableName.Should().Be(TestDestinationTableName);
            target.Columns.Should().Equal(
                new ColumnMap("PublicInt", "public_int"),
                new ColumnMap("PublicBool", "public_bool"),
                new ColumnMap("BaseInt", "base_int"),
                new ColumnMap("PublicString", "public_string"));
        }

        [Test]
        public async Task SaveAsync_ShouldMapModelPropertiesByName_WhenNoMappingIsRegistered()
        {
            await _bulkProcessor.SaveAsync(new[] { new ValidModelObjectB() }, TestDestinationTableName);

            _provider.Targets.Single().Columns.Select(column => column.SourceColumn)
                .Should().BeEquivalentTo(nameof(ValidModelObjectB.AnotherColumn), nameof(ValidModelObjectB.BaseInt));
            _provider.Targets.Single().Columns.Should().OnlyContain(column => column.SourceColumn == column.DestinationColumn);
        }

        [Test]
        public async Task SaveAsync_ShouldThrowArgumentExceptionBeforeOpeningAConnection_WhenAModelMappingMapsAGetOnlyProperty()
        {
            Func<Task> action = () => _bulkProcessor.SaveAsync(new[] { new ModelWithComputedProperty() }, TestDestinationTableName);

            await action.Should().ThrowAsync<ArgumentException>()
                .WithMessage($"{BulkCopyErrorMessages.SourceColumnNotFound(nameof(ModelWithComputedProperty.Reference))}*");
            _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task SaveAsync_ShouldUseTheDataTableNameAsTheMappingKey_WhenTheTableIsNamed()
        {
            DataTable table = GivenADataTableWithRows("PublicInt", "PublicBool", "BaseInt", "PublicString");
            table.TableName = "ValidDataMapping1";

            await _bulkProcessor.SaveAsync(table, "TableB");

            _provider.Targets.Single().Columns.Select(column => column.DestinationColumn)
                .Should().Equal("public_int", "public_bool", "base_int", "public_string");
        }

        [Test]
        public async Task SaveAsync_ShouldUseTheDestinationAsTheMappingKey_WhenTheDataTableHasNoName()
        {
            await _bulkProcessor.SaveAsync(GivenADataTableWithRows("BaseInt", "AnotherColumn"), "TableB");

            _provider.Targets.Single().Columns.Should().Equal(
                new ColumnMap("BaseInt", "base_int"), new ColumnMap("AnotherColumn", "another_column"));
        }

        [Test]
        public async Task SaveAsync_ShouldMapDataTableColumnsByName_WhenNoMappingMatches()
        {
            await _bulkProcessor.SaveAsync(GivenADataTableWithRows("ColA"), TestDestinationTableName);

            _provider.Targets.Single().Columns.Should().Equal(new ColumnMap("ColA", "ColA"));
        }

        [Test]
        public async Task SaveAsync_ShouldUseTheSourceNameAsTheMappingKey_WhenOneIsGivenForAReader()
        {
            using IDataReader reader = GivenADataTableWithRows("BaseInt", "AnotherColumn").CreateDataReader();

            await _bulkProcessor.SaveAsync(reader, "SomeOtherTable", "TableB");

            _provider.Targets.Single().Columns.Should().Equal(
                new ColumnMap("BaseInt", "base_int"), new ColumnMap("AnotherColumn", "another_column"));
        }

        [TestCase(null)]
        [TestCase(" ")]
        public async Task SaveAsync_ShouldUseTheDestinationAsTheMappingKey_WhenAReaderHasNoSourceName(string sourceName)
        {
            using IDataReader reader = GivenADataTableWithRows("BaseInt", "AnotherColumn").CreateDataReader();

            await _bulkProcessor.SaveAsync(reader, "TableB", sourceName);

            _provider.Targets.Single().Columns.Select(column => column.DestinationColumn).Should().Equal("base_int", "another_column");
        }

        [Test]
        public async Task SaveAsync_ShouldUseTheJsonTableNameAsDestinationAndMappingKey()
        {
            await _bulkProcessor.SaveAsync(JsonStream("TableB"));

            BulkWriteTarget target = _provider.Targets.Single();
            target.TableName.Should().Be("TableB");
            target.Columns.Select(column => column.DestinationColumn).Should().Equal("base_int", "another_column");
        }

        [Test]
        public async Task SaveAsync_ShouldDisposeTheReadersItCreates()
        {
            await _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);
            await _bulkProcessor.SaveAsync(GivenADataTableWithRows("ColA"), TestDestinationTableName);
            await _bulkProcessor.SaveAsync(JsonStream("TableB"));

            _provider.Readers.Should().HaveCount(3).And.OnlyContain(reader => reader.IsClosed);
        }

        [Test]
        public async Task SaveAsync_ShouldDisposeTheReadersItCreates_WhenTheWriteFails()
        {
            _provider.WriteException = new InvalidOperationException("driver failure");

            Func<Task> action = () => _bulkProcessor.SaveAsync(GivenADataTableWithRows("ColA"), TestDestinationTableName);

            await action.Should().ThrowAsync<InvalidOperationException>();
            _provider.Readers.Single().IsClosed.Should().BeTrue();
        }

        [Test]
        public async Task SaveAsync_ShouldNotDisposeTheCallersReader()
        {
            var reader = new Mock<IDataReader>();
            reader.SetupGet(r => r.FieldCount).Returns(1);
            reader.Setup(r => r.GetName(0)).Returns("ColA");

            await _bulkProcessor.SaveAsync(reader.Object, TestDestinationTableName);

            reader.Verify(r => r.Dispose(), Times.Never);
            reader.Verify(r => r.Close(), Times.Never);
        }

        [Test]
        public async Task SaveAsync_ShouldPassTheCancellationTokenToTheWriter()
        {
            using var cancellation = new CancellationTokenSource();

            await _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName, cancellation.Token);

            _provider.Writer.Verify(w => w.WriteAsync(It.IsAny<IDataReader>(), It.IsAny<BulkWriteTarget>(), cancellation.Token), Times.Once);
        }

        [Test]
        public async Task SaveAsync_ShouldLogStartAndCompletionWithoutTheConnectionString()
        {
            await _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);

            _loggerFactory.Contains(LogLevel.Information, $"Starting bulk copy to {TestDestinationTableName} via FakeDb").Should().BeTrue();
            _loggerFactory.Contains(LogLevel.Information, $"Completed bulk copy to {TestDestinationTableName} via FakeDb").Should().BeTrue();
            _loggerFactory.Entries.Should().NotContain(entry => entry.Message.Contains("secret"));
        }

        [Test]
        public async Task SaveAsync_ShouldLogTheErrorAndRethrow_WhenTheWriteFails()
        {
            _provider.WriteException = new InvalidOperationException("driver failure");

            Func<Task> action = () => _bulkProcessor.SaveAsync(ModelObjects, TestDestinationTableName);

            var thrown = await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("translated");
            _loggerFactory.Entries.Should().Contain(entry =>
                entry.Level == LogLevel.Error && entry.Exception == thrown.Which && entry.Message.Contains("failed"));
        }

        private IBulkProcessor CreateProcessor(MappingRegistry mappings) =>
            new BulkProcessor(_provider.Object, ConnectionString, mappings, _loggerFactory);

        private FakeDbTransaction GivenTheProcessorUsesAnExternalTransaction()
        {
            var connection = new FakeDbConnection();
            var transaction = new FakeDbTransaction(connection);
            _provider.AcceptExternal(transaction);
            _bulkProcessor.Transaction = transaction;
            return transaction;
        }

        private static async Task TheCallShouldThrowForTheDestination(Func<Task> action)
        {
            await action.Should().ThrowAsync<ArgumentException>()
                .WithMessage("destinationTableName must not be null or empty. (Parameter 'destinationTableName')");
        }

        private static DataTable GivenADataTableWithRows(params string[] columnNames)
        {
            var dataTable = new DataTable();
            foreach (var columnName in columnNames)
            {
                dataTable.Columns.Add(new DataColumn(columnName, typeof(int)));
            }

            var dataRow = dataTable.NewRow();
            foreach (var columnName in columnNames)
            {
                dataRow[columnName] = 1;
            }

            dataTable.Rows.Add(dataRow);
            return dataTable;
        }

        private static Stream JsonStream(string tableName) => new MemoryStream(Encoding.UTF8.GetBytes(
            $$"""
            {
              "tableName": "{{tableName}}",
              "columns": [ { "name": "BaseInt", "type": "int" }, { "name": "AnotherColumn", "type": "int" } ],
              "data": [ { "BaseInt": 1, "AnotherColumn": 2 } ]
            }
            """));
    }
}

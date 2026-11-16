using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The caller's OracleTransaction is joined and never committed (the writer also sets OracleCommand.Transaction), other transaction
/// types are rejected, DirectPath is still refused at registration (until step 8), and Loki logs the Oracle provider name.
/// </summary>
public sealed partial class OracleArrayBindTests
{
    private const string CountTestDbModels = "SELECT COUNT(*) FROM TestDbModels";

    [Test]
    public async Task SaveAsync_ShouldWriteInsideTheCallersTransactionWithoutCommittingIt_WhenTheCallerSetsOne()
    {
        await using OracleConnection connection = await _database.OpenConnectionAsync();
        await using OracleTransaction transaction = connection.BeginTransaction();
        IBulkProcessor processor = Processor();
        processor.Transaction = transaction;

        await processor.SaveAsync(TwoTestDbModelRows(), "TestDbModels");

        (await CountAsync(connection)).Should().Be(2, "the rows are visible inside the caller's transaction");
        (await _database.ReadTestDbModelsAsync()).Should().BeEmpty("Loki must not commit the caller's transaction");
        transaction.Commit();
        (await _database.ReadTestDbModelsAsync()).Should().HaveCount(2);
    }

    [Test]
    public async Task Transaction_ShouldThrowArgumentExceptionAndLeaveTheTransactionAlone_WhenItIsNotAnOracleTransaction()
    {
        var foreignTransaction = new ForeignDbTransaction();
        IBulkProcessor processor = Processor();

        Action action = () => processor.Transaction = foreignTransaction;

        action.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("transaction");
        processor.Transaction.Should().BeNull();
        foreignTransaction.WasTouched.Should().BeFalse("Loki never commits, rolls back or disposes a caller's transaction");
        await processor.SaveAsync(TwoTestDbModelRows(), "TestDbModels");
        (await _database.ReadTestDbModelsAsync()).Should().HaveCount(2);
    }

    [Test]
    public void AddOracle_ShouldStillRejectDirectPathAtRegistration_UntilStep8AddsIt()
    {
        var services = new ServiceCollection();

        Action action = () => services.AddLokiBulkDataProcessor(_ => { }).AddOracle(options => options.WriteMode = OracleWriteMode.DirectPath);

        action.Should().Throw<NotSupportedException>().WithMessage("*OracleWriteMode.DirectPath is not available*");
    }

    [Test]
    public async Task SaveAsync_ShouldLogTheStartAndCompletionViaOracle_WhenSavingData()
    {
        var loggerFactory = new TestLoggerFactory();
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(options => options.LoggerFactory = loggerFactory).AddOracle();
        await using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IBulkProcessorFactory>().Create(OracleProvider.Name, _database.ConnectionString)
            .SaveAsync(TwoTestDbModelRows(), "TestDbModels");

        loggerFactory.Contains(LogLevel.Information, "Starting bulk copy to TestDbModels via Oracle").Should().BeTrue();
        loggerFactory.Contains(LogLevel.Information, "Completed bulk copy to TestDbModels via Oracle").Should().BeTrue();
    }

    private static async Task<int> CountAsync(OracleConnection connection) =>
        (await OracleTestDatabase.QueryAsync(connection, CountTestDbModels, reader => Convert.ToInt32(reader.GetValue(0))))[0];

    /// <summary>A transaction from another driver; records any call Loki makes on it.</summary>
    private sealed class ForeignDbTransaction : DbTransaction
    {
        public bool WasTouched { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => null;

        public override void Commit() => WasTouched = true;

        public override void Rollback() => WasTouched = true;

        protected override void Dispose(bool disposing) => WasTouched = true;
    }
}

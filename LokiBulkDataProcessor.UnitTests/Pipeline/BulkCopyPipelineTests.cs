using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Pipeline;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Pipeline;

public class BulkCopyPipelineTests
{
    private const string ConnectionString = "Server=fake;Password=secret";
    private static readonly BulkWriteTarget Target = new("Dest", new[] { new ColumnMap("A", "B") });

    private FakeProvider _provider;
    private RecordingLoggerFactory _loggerFactory;
    private BulkCopyPipeline _pipeline;
    private IDataReader _reader;

    [SetUp]
    public void SetUp()
    {
        _provider = new FakeProvider();
        _loggerFactory = new RecordingLoggerFactory();
        _pipeline = new BulkCopyPipeline(_provider.Object, _loggerFactory);
        _reader = new Mock<IDataReader>().Object;
    }

    [Test]
    public async Task ExecuteAsync_ShouldCommitThenDisposeOwnTransactionAndConnection_WhenWriteSucceeds()
    {
        await ExecuteOwnAsync();

        FakeDbTransaction transaction = _provider.Connection.LastTransaction;
        transaction.CommitCount.Should().Be(1);
        transaction.RollbackCount.Should().Be(0);
        transaction.DisposeCount.Should().Be(1);
        _provider.Connection.OpenCount.Should().Be(1);
        _provider.Connection.DisposeCount.Should().Be(1);
        _provider.Mock.Verify(p => p.CreateConnection(ConnectionString), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldRollBackAndDisposeOwnTransactionAndConnection_WhenWriteFails()
    {
        _provider.WriteException = new InvalidOperationException("driver failure");

        await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>();

        FakeDbTransaction transaction = _provider.Connection.LastTransaction;
        transaction.CommitCount.Should().Be(0);
        transaction.RollbackCount.Should().Be(1);
        transaction.DisposeCount.Should().Be(1);
        _provider.Connection.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_ShouldRethrowTheWriteErrorAndLogTheRollbackError_WhenRollbackThrows()
    {
        var rollbackError = new InvalidOperationException("rollback failure");
        _provider.Connection.NextTransaction = new FakeDbTransaction(_provider.Connection) { RollbackException = rollbackError };
        var writeError = new InvalidOperationException("driver failure");
        _provider.WriteException = writeError;

        var thrown = await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.InnerException.Should().BeSameAs(writeError);
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.Exception == rollbackError);
        _provider.Connection.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_ShouldRollBackAndRethrowTheCommitError_WhenCommitFails()
    {
        var commitError = new InvalidOperationException("commit failure");
        _provider.Connection.NextTransaction = new FakeDbTransaction(_provider.Connection) { CommitException = commitError };

        var thrown = await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(commitError);
        _provider.Connection.LastTransaction.RollbackCount.Should().Be(1);
        _provider.Connection.LastTransaction.DisposeCount.Should().Be(1);
        _provider.Connection.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotCommitRollBackOrDisposeTheCallersTransaction_WhenWriteSucceeds()
    {
        var (external, connection) = GivenAnExternalTransaction();

        await ExecuteExternalAsync(external);

        TheCallersTransactionShouldBeUntouched(external, connection);
        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotRollBackOrDisposeTheCallersTransaction_WhenWriteFails()
    {
        var (external, connection) = GivenAnExternalTransaction();
        _provider.WriteException = new InvalidOperationException("driver failure");

        await ((Func<Task>)(() => ExecuteExternalAsync(external))).Should().ThrowAsync<InvalidOperationException>();

        TheCallersTransactionShouldBeUntouched(external, connection);
        _loggerFactory.Contains(LogLevel.Warning, "caller-owned transaction").Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_ShouldWriteThroughTheCallersConnectionAndTransaction_WhenTransactionIsExternal()
    {
        var (external, connection) = GivenAnExternalTransaction();

        await ExecuteExternalAsync(external);

        _provider.Contexts.Should().ContainSingle();
        _provider.Contexts[0].Connection.Should().BeSameAs(connection);
        _provider.Contexts[0].Transaction.Should().BeSameAs(external);
    }

    [Test]
    public async Task ExecuteAsync_ShouldThrowExternalTransactionCompleted_WhenTheCallersTransactionHasNoConnection()
    {
        var (external, _) = GivenAnExternalTransaction();
        external.Detach();

        await ((Func<Task>)(() => ExecuteExternalAsync(external))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(BulkCopyErrorMessages.ExternalTransactionCompleted);

        _provider.Mock.Verify(p => p.CreateWriter(It.IsAny<BulkWriteContext>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotOpenAnything_WhenTheProviderRejectsTheCallersTransaction()
    {
        _provider.RejectExternal();
        var external = new FakeDbTransaction(new FakeDbConnection());

        await ((Func<Task>)(() => ExecuteExternalAsync(external))).Should().ThrowAsync<ArgumentException>();

        _provider.Mock.Verify(p => p.CreateConnection(It.IsAny<string>()), Times.Never);
        _provider.Mock.Verify(p => p.CreateWriter(It.IsAny<BulkWriteContext>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldDisposeTheConnection_WhenOpenThrows()
    {
        _provider.Connection.OpenException = new InvalidOperationException("cannot open");

        await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>().WithMessage("cannot open");

        _provider.Connection.DisposeCount.Should().Be(1);
        _provider.Mock.Verify(p => p.CreateWriter(It.IsAny<BulkWriteContext>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldDisposeTheConnection_WhenBeginTransactionThrows()
    {
        _provider.Connection.BeginTransactionException = new InvalidOperationException("cannot begin");

        await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>().WithMessage("cannot begin");

        _provider.Connection.DisposeCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_ShouldDisposeTheWriter_WhenWriteSucceedsOrFails()
    {
        await ExecuteOwnAsync();
        _provider.Connection = new FakeDbConnection();
        _provider.WriteException = new InvalidOperationException("driver failure");
        await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>();

        _provider.Writer.Verify(w => w.DisposeAsync(), Times.Exactly(2));
    }

    [Test]
    public async Task ExecuteAsync_ShouldThrowTheTranslatedExceptionWithTheOriginalAsInner_WhenWriteFails()
    {
        var driverError = new InvalidOperationException("driver failure");
        _provider.WriteException = driverError;

        var thrown = await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>().WithMessage("translated");

        thrown.Which.InnerException.Should().BeSameAs(driverError);
        _provider.Mock.Verify(p => p.TranslateException(driverError, Target), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldAttachTheOriginalAsInner_WhenTheProviderTranslationDropsIt()
    {
        var driverError = new InvalidOperationException("driver failure");
        _provider.WriteException = driverError;
        _provider.Mock.Setup(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()))
            .Returns(new InvalidOperationException("translated without inner"));

        var thrown = await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("translated without inner");

        thrown.Which.InnerException.Should().BeSameAs(driverError);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ExecuteAsync_ShouldRethrowTheOriginal_WhenTheProviderReturnsNullOrTheSameException(bool returnNull)
    {
        var driverError = new InvalidOperationException("driver failure");
        _provider.WriteException = driverError;
        _provider.Mock.Setup(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()))
            .Returns(returnNull ? null : driverError);

        var thrown = await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<InvalidOperationException>();

        thrown.Which.Should().BeSameAs(driverError);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotTranslate_WhenTheWriteIsCancelled()
    {
        _provider.WriteException = new OperationCanceledException();

        await ((Func<Task>)ExecuteOwnAsync).Should().ThrowAsync<OperationCanceledException>();

        _provider.Mock.Verify(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()), Times.Never);
        _provider.Connection.LastTransaction.RollbackCount.Should().Be(1);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotTranslate_WhenTheTokenIsCancelledAndTheDriverThrowsSomethingElse()
    {
        using var cancellation = new CancellationTokenSource();
        _provider.Writer.Setup(w => w.WriteAsync(It.IsAny<IDataReader>(), It.IsAny<BulkWriteTarget>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromException(new InvalidOperationException("Operation cancelled by user."));
            });

        await ((Func<Task>)(() => ExecuteOwnAsync(cancellation.Token))).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Operation cancelled by user.");

        _provider.Mock.Verify(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotOpenAConnection_WhenTheTokenIsAlreadyCancelled()
    {
        await ((Func<Task>)(() => ExecuteOwnAsync(new CancellationToken(canceled: true)))).Should().ThrowAsync<OperationCanceledException>();

        _provider.Connection.OpenCount.Should().Be(0);
        _provider.Connection.DisposeCount.Should().Be(1);
        _provider.Mock.Verify(p => p.CreateWriter(It.IsAny<BulkWriteContext>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldPassTheCallSettingsAndOpenTransactionToTheWriter()
    {
        await _pipeline.ExecuteAsync(_reader, Target, new BulkCallSettings(ConnectionString, 250, 77, null), CancellationToken.None);

        BulkWriteContext context = _provider.Contexts.Should().ContainSingle().Subject;
        context.Options.Should().Be(new BulkWriteOptions(250, 77));
        context.Connection.Should().BeSameAs(_provider.Connection);
        context.Transaction.Should().BeSameAs(_provider.Connection.LastTransaction);
        context.LoggerFactory.Should().BeSameAs(_loggerFactory);
        _provider.Readers.Should().ContainSingle().Which.Should().BeSameAs(_reader);
    }

    [Test]
    public void BulkCallSettings_ToString_ShouldNotContainTheConnectionString()
    {
        new BulkCallSettings(ConnectionString, 1, 2, null).ToString().Should().NotContain("secret").And.NotContain("Server");
    }

    private Task ExecuteOwnAsync() => ExecuteOwnAsync(CancellationToken.None);

    private Task ExecuteOwnAsync(CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(_reader, Target, new BulkCallSettings(ConnectionString, 10, 20, null), cancellationToken);

    private Task ExecuteExternalAsync(IDbTransaction external) =>
        _pipeline.ExecuteAsync(_reader, Target, new BulkCallSettings(ConnectionString, 10, 20, external), CancellationToken.None);

    private (FakeDbTransaction Transaction, FakeDbConnection Connection) GivenAnExternalTransaction()
    {
        var connection = new FakeDbConnection();
        connection.Open();
        var transaction = new FakeDbTransaction(connection);
        _provider.AcceptExternal(transaction);
        return (transaction, connection);
    }

    private static void TheCallersTransactionShouldBeUntouched(FakeDbTransaction transaction, FakeDbConnection connection)
    {
        transaction.CommitCount.Should().Be(0);
        transaction.RollbackCount.Should().Be(0);
        transaction.DisposeCount.Should().Be(0);
        connection.DisposeCount.Should().Be(0);
        connection.State.Should().Be(ConnectionState.Open);
    }
}

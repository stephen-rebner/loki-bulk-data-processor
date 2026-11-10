using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Pipeline;

/// <summary>
/// The connection and transaction for one call, and who owns them. Loki's own transaction is committed or rolled back
/// and disposed with its connection; a caller-owned transaction and its connection are never touched (#12, #13).
/// </summary>
internal sealed class ConnectionScope : IAsyncDisposable
{
    private readonly ILogger _logger;

    private ConnectionScope(DbConnection connection, DbTransaction transaction, bool ownsTransaction, ILogger logger)
    {
        Connection = connection;
        Transaction = transaction;
        OwnsTransaction = ownsTransaction;
        _logger = logger;
    }

    public DbConnection Connection { get; }

    public DbTransaction Transaction { get; }

    public bool OwnsTransaction { get; }

    /// <summary>Joins the caller's transaction, or opens a new connection and begins Loki's own transaction.</summary>
    public static Task<ConnectionScope> OpenAsync(IBulkCopyProvider provider, BulkCallSettings settings,
        ILogger logger, CancellationToken cancellationToken) =>
        settings.ExternalTransaction is not null
            ? Task.FromResult(Adopt(provider, settings.ExternalTransaction, logger))
            : OpenOwnedAsync(provider, settings.ConnectionString, logger, cancellationToken);

    /// <summary>Commits only Loki's own transaction. Not cancellable once the write has succeeded.</summary>
    public Task CompleteAsync() =>
        OwnsTransaction ? Transaction.CommitAsync(CancellationToken.None) : Task.CompletedTask;

    /// <summary>Rolls back only Loki's own transaction. Never throws, so the original failure is the one that surfaces (#12).</summary>
    public async Task RollbackAfterFailureAsync()
    {
        if (!OwnsTransaction)
        {
            _logger.LogWarning("Bulk copy failed inside a caller-owned transaction; it was left for the caller to commit or roll back");
            return;
        }

        try
        {
            await Transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException, "Rolling back the internal transaction failed; the original bulk copy error is rethrown");
        }
    }

    /// <summary>
    /// Disposes Loki's own transaction and then its connection; does nothing for a caller-owned transaction. Never throws:
    /// it runs under <c>await using</c> while a write or commit error may be in flight, and that error must surface. On the
    /// success path the commit has already happened, so a dispose failure is logged and the call still succeeds.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!OwnsTransaction)
        {
            return;
        }

        await DisposeAndLogFailureAsync(Transaction, "transaction").ConfigureAwait(false);
        await DisposeAndLogFailureAsync(Connection, "connection").ConfigureAwait(false);
    }

    /// <summary>Logs the exception TYPE only, never the exception object: driver messages can echo row values (CWE-532).</summary>
    private async ValueTask DisposeAndLogFailureAsync(IAsyncDisposable resource, string resourceName)
    {
        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception disposeException)
        {
            _logger.LogError("Disposing the internal {Resource} failed with {ExceptionType}; the error was not rethrown",
                resourceName, disposeException.GetType().FullName);
        }
    }

    private static ConnectionScope Adopt(IBulkCopyProvider provider, IDbTransaction external, ILogger logger)
    {
        DbTransaction transaction = provider.AdoptExternalTransaction(external);
        DbConnection connection = transaction.Connection
            ?? throw new InvalidOperationException(BulkCopyErrorMessages.ExternalTransactionCompleted);
        return new ConnectionScope(connection, transaction, ownsTransaction: false, logger);
    }

    private static async Task<ConnectionScope> OpenOwnedAsync(IBulkCopyProvider provider, string connectionString,
        ILogger logger, CancellationToken cancellationToken)
    {
        DbConnection connection = provider.CreateConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new ConnectionScope(connection, transaction, ownsTransaction: true, logger);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);   // #13: no leak if Open or BeginTransaction fails
            throw;
        }
    }
}

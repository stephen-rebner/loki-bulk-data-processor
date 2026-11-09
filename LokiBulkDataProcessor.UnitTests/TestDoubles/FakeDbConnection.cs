using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>A DbConnection that records Open/Dispose and can be told to fail, for pipeline ownership tests.</summary>
internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public Exception OpenException { get; set; }

    public Exception BeginTransactionException { get; set; }

    /// <summary>When set, OpenAsync waits for it, so a test can act while a call is in flight.</summary>
    public Task OpenGate { get; set; }

    /// <summary>The transaction returned by the next BeginTransaction (a new one when null).</summary>
    public FakeDbTransaction NextTransaction { get; set; }

    public FakeDbTransaction LastTransaction { get; private set; }

    public int OpenCount { get; private set; }

    public int DisposeCount { get; private set; }

    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => "FakeDatabase";

    public override string DataSource => "FakeServer";

    public override string ServerVersion => "1.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open()
    {
        OpenCount++;
        if (OpenException is not null)
        {
            throw OpenException;
        }

        _state = ConnectionState.Open;
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (OpenGate is not null)
        {
            await OpenGate;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Open();
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        if (BeginTransactionException is not null)
        {
            throw BeginTransactionException;
        }

        LastTransaction = NextTransaction ?? new FakeDbTransaction(this);
        return LastTransaction;
    }

    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
            _state = ConnectionState.Closed;
        }

        base.Dispose(disposing);
    }
}

using System;
using System.Data;
using System.Data.Common;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>A DbTransaction that records Commit/Rollback/Dispose and can be told to fail.</summary>
internal sealed class FakeDbTransaction(DbConnection connection) : DbTransaction
{
    private DbConnection _connection = connection;

    public Exception CommitException { get; set; }

    public Exception RollbackException { get; set; }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public int DisposeCount { get; private set; }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection DbConnection => _connection;

    /// <summary>Simulates a completed transaction: like SqlTransaction, its Connection becomes null.</summary>
    public void Detach() => _connection = null;

    public override void Commit()
    {
        CommitCount++;
        if (CommitException is not null)
        {
            throw CommitException;
        }
    }

    public override void Rollback()
    {
        RollbackCount++;
        if (RollbackException is not null)
        {
            throw RollbackException;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
        }

        base.Dispose(disposing);
    }
}

using System;
using System.Data;
using System.Data.Common;

namespace Loki.BulkDataProcessor.Providers;

/// <summary>
/// A database engine that Loki can bulk-copy into. Register one with the builder returned by
/// <c>AddLokiBulkDataProcessor</c> (for example <c>.AddSqlServer()</c>).
/// Implementations must be stateless and thread-safe: a single instance serves every processor.
/// </summary>
public interface IBulkCopyProvider
{
    /// <summary>Registered name used by <see cref="IBulkProcessorFactory.Create"/> (case-insensitive).</summary>
    string Name { get; }

    /// <summary>Creates a new, unopened connection. Loki opens, owns and disposes it.</summary>
    DbConnection CreateConnection(string connectionString);

    /// <summary>
    /// Validates and returns a caller-owned transaction as this provider's type. Loki never commits,
    /// rolls back or disposes it. Throws <see cref="ArgumentException"/> when the transaction belongs to another driver.
    /// </summary>
    DbTransaction AdoptExternalTransaction(IDbTransaction transaction);

    /// <summary>Creates a writer bound to the context's open connection and active transaction.</summary>
    IBulkWriter CreateWriter(BulkWriteContext context);

    /// <summary>
    /// Translates a write failure into a provider-neutral exception. Must return a NEW exception whose
    /// <see cref="Exception.InnerException"/> is <paramref name="exception"/>, so the driver error is never lost.
    /// </summary>
    Exception TranslateException(Exception exception, BulkWriteTarget target);
}

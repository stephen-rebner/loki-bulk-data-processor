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
    /// Translates a write failure into a provider-neutral exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Return a NEW exception whose <see cref="Exception.InnerException"/> chain contains <paramref name="exception"/>, so the
    /// driver error is never lost (#15). Keep its message fixed and data-free: driver messages can quote row values.
    /// </para>
    /// <para>
    /// Return <paramref name="exception"/> itself only for an error your provider raised before writing (for example a rejected
    /// table or column name) whose message is already safe to show; the pipeline then rethrows it unchanged. Returning null does
    /// the same. Never return a driver exception or an exception from the caller's data unchanged.
    /// </para>
    /// <para>
    /// If the returned exception does not carry <paramref name="exception"/> as an InnerException, or this method throws, the
    /// pipeline throws an <see cref="InvalidOperationException"/> with <see cref="BulkCopyErrorMessages.BulkCopyFailed(string)"/>
    /// and <paramref name="exception"/> as InnerException instead. Not called when the save was cancelled: the pipeline then
    /// throws an <see cref="OperationCanceledException"/> with <paramref name="exception"/> as InnerException.
    /// </para>
    /// </remarks>
    Exception TranslateException(Exception exception, BulkWriteTarget target);
}

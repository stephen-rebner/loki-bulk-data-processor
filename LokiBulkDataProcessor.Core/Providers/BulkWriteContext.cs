using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Providers;

/// <summary>Everything a writer needs for one call. The connection and transaction are owned by Loki or by the caller, never by the writer.</summary>
/// <param name="Connection">Open connection to write through. Do not close or dispose it.</param>
/// <param name="Transaction">Active transaction to enlist in. Do not commit, roll back or dispose it.</param>
/// <param name="Options">Batch size and timeout for this call.</param>
/// <param name="LoggerFactory">Loki's logger factory (a no-op factory unless the caller opted in to logging).</param>
public sealed record BulkWriteContext(DbConnection Connection, DbTransaction Transaction, BulkWriteOptions Options, ILoggerFactory LoggerFactory);

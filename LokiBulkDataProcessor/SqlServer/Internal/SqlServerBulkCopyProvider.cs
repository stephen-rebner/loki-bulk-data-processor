using System;
using System.Data;
using System.Data.Common;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Data.SqlClient;

namespace Loki.BulkDataProcessor.SqlServer.Internal;

/// <summary>SQL Server implementation of the provider SPI. Stateless and thread-safe.</summary>
internal sealed class SqlServerBulkCopyProvider(string name) : IBulkCopyProvider
{
    public string Name { get; } = name;

    public DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    public DbTransaction AdoptExternalTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction as SqlTransaction ?? throw new ArgumentException(
            $"A SQL Server bulk processor requires a Microsoft.Data.SqlClient.SqlTransaction but received {transaction.GetType().FullName}. " +
            "System.Data.SqlClient is not supported from 9.0.", nameof(transaction));
    }

    public IBulkWriter CreateWriter(BulkWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SqlServerBulkWriter((SqlConnection)context.Connection, (SqlTransaction)context.Transaction, context.Options);
    }

    public Exception TranslateException(Exception exception, BulkWriteTarget target) =>
        SqlServerExceptionTranslator.Translate(exception);
}

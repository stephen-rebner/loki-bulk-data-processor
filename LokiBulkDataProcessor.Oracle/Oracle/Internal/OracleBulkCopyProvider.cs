using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Types;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal;

/// <summary>Oracle implementation of the provider SPI. Immutable, stateless per call and thread-safe.</summary>
internal sealed class OracleBulkCopyProvider : IBulkCopyProvider
{
    private readonly IOracleIdentifierResolver _resolver;
    private readonly OracleTypeMap _typeMap;

    /// <param name="name">Registered provider name.</param>
    /// <param name="options">Already validated options (<see cref="OracleProviderOptions.Validated"/>).</param>
    public OracleBulkCopyProvider(string name, OracleProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Name = name;
        Options = options;
        _resolver = CreateResolver(options.IdentifierResolution);
        _typeMap = new OracleTypeMap(options.BooleanBinding);
    }

    public string Name { get; }

    internal OracleProviderOptions Options { get; }

    internal IOracleIdentifierResolver Resolver => _resolver;

    internal OracleTypeMap TypeMap => _typeMap;

    public DbConnection CreateConnection(string connectionString) => new OracleConnection(connectionString);

    public DbTransaction AdoptExternalTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction as OracleTransaction ?? throw new ArgumentException(
            $"An Oracle bulk processor requires an Oracle.ManagedDataAccess.Client.OracleTransaction but received {transaction.GetType().FullName}.",
            nameof(transaction));
    }

    /// <summary>The writer for the registered <see cref="OracleWriteMode"/>.</summary>
    public IBulkWriter CreateWriter(BulkWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ILoggerFactory loggerFactory = context.LoggerFactory ?? NullLoggerFactory.Instance;
        var connection = (OracleConnection)context.Connection;
        Debug.Assert(context.Transaction is null || ReferenceEquals(context.Transaction.Connection, context.Connection),
            "The pipeline always passes the transaction of the context's own connection.");
        ThrowIfDirectPathWithoutTransaction(context);
        return Options.WriteMode == OracleWriteMode.DirectPath
            ? new DirectPathOracleWriter(connection, context.Options, _resolver, _typeMap, Name, loggerFactory.CreateLogger<DirectPathOracleWriter>())
            : new ArrayBindOracleWriter(connection, context.Transaction as OracleTransaction, context.Options, _resolver, _typeMap,
                loggerFactory.CreateLogger<ArrayBindOracleWriter>());
    }

    internal const string DirectPathNeedsTransactionMessage =
        "A direct-path load needs an active transaction (without one OracleBulkCopy commits the load itself, so it could not be rolled back).";

    /// <summary>The pipeline always passes a transaction; this guards the invariant in release builds too (see DirectPathOracleWriter).</summary>
    private void ThrowIfDirectPathWithoutTransaction(BulkWriteContext context)
    {
        if (Options.WriteMode == OracleWriteMode.DirectPath && context.Transaction is null)
        {
            throw OracleValidation.InvalidOperation(DirectPathNeedsTransactionMessage);
        }
    }

    public Exception TranslateException(Exception exception, BulkWriteTarget target) =>
        OracleExceptionTranslator.Translate(exception, target, Options.IdentifierResolution);

    private static IOracleIdentifierResolver CreateResolver(OracleIdentifierResolution resolution) =>
        resolution == OracleIdentifierResolution.CatalogLookup
            ? new CatalogIdentifierResolver()
            : new UnquotedIdentifierResolver();
}

using System;
using System.Data;
using System.Data.Common;
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

    /// <summary>Only the array-bind writer exists until direct path lands; <see cref="OracleProviderOptions.Validated"/> rejects DirectPath.</summary>
    public IBulkWriter CreateWriter(BulkWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ILogger logger = (context.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ArrayBindOracleWriter>();
        return new ArrayBindOracleWriter((OracleConnection)context.Connection, context.Transaction as OracleTransaction,
            context.Options, _resolver, _typeMap, logger);
    }

    public Exception TranslateException(Exception exception, BulkWriteTarget target) =>
        OracleExceptionTranslator.Translate(exception, target, Options.IdentifierResolution);

    private static IOracleIdentifierResolver CreateResolver(OracleIdentifierResolution resolution) =>
        resolution == OracleIdentifierResolution.CatalogLookup
            ? new CatalogIdentifierResolver()
            : new UnquotedIdentifierResolver();
}

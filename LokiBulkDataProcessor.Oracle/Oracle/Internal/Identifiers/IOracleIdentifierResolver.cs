using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Providers;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Identifiers;

/// <summary>Turns the caller's table and column names into quoted SQL identifiers. Stateless; one instance per provider.</summary>
internal interface IOracleIdentifierResolver
{
    /// <summary>Resolves <paramref name="target"/>. Throws a marked validation exception for a name it cannot accept.</summary>
    /// <param name="connection">Open connection (inside the call's transaction); used only by resolvers that query the catalogue.</param>
    Task<OracleResolvedTarget> ResolveAsync(OracleConnection connection, BulkWriteTarget target, CancellationToken cancellationToken);
}

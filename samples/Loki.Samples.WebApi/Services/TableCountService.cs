using System.Data.Common;
using Loki.Samples.WebApi.Engines;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace Loki.Samples.WebApi.Services;

/// <summary>Counts the rows of the sample tables, so you can see what each request saved (or did not).</summary>
internal sealed class TableCountService(EngineCatalog engines)
{
    public async Task<IReadOnlyDictionary<string, long>> CountAsync(DatabaseEngine engine, CancellationToken ct)
    {
        await using DbConnection connection = CreateConnection(engine, engines.ConnectionStringFor(engine));
        await connection.OpenAsync(ct);
        Dictionary<string, long> counts = new(StringComparer.Ordinal);
        foreach ((string table, string countQuery) in ImportTables.CountQueriesFor(engine))
        {
            counts[table] = await CountAsync(connection, countQuery, ct);
        }

        return counts;
    }

    private static async Task<long> CountAsync(DbConnection connection, string countQuery, CancellationToken ct)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = countQuery;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static DbConnection CreateConnection(DatabaseEngine engine, string connectionString) => engine switch
    {
        DatabaseEngine.SqlServer => new SqlConnection(connectionString),
        DatabaseEngine.Oracle => new OracleConnection(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Not a known engine.")
    };
}

using System.Data.Common;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Loki.Samples.Benchmark;

/// <summary>One database under test: its Loki provider, its EF Core context, and constant SQL to empty and count the table.</summary>
internal abstract class BenchmarkTarget(string connectionString)
{
    private const string EmptyTable = "TRUNCATE TABLE BenchmarkRows";
    private const string CountRows = "SELECT COUNT(*) FROM BenchmarkRows";

    public string ConnectionString { get; } = connectionString;

    public abstract string Engine { get; }

    /// <summary>The Loki provider name: "SqlServer" (SqlBulkCopy) or "Oracle" (array bind, the default write mode).</summary>
    public abstract string LokiProvider { get; }

    public abstract BenchmarkDbContext CreateDbContext();

    /// <summary>Checks (or, on SQL Server, creates) the database and table before anything is timed.</summary>
    public abstract Task PrepareAsync(CancellationToken ct);

    public Task EmptyTableAsync(CancellationToken ct) => ExecuteAsync(EmptyTable, ct);

    public async Task<long> CountRowsAsync(CancellationToken ct) => Convert.ToInt64(await ExecuteAsync(CountRows, ct));

    protected abstract DbConnection CreateConnection();

    protected async Task<object?> ExecuteAsync(string sql, CancellationToken ct)
    {
        await using DbConnection connection = CreateConnection();
        await connection.OpenAsync(ct);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }
}

internal sealed class SqlServerTarget(string connectionString) : BenchmarkTarget(connectionString)
{
    public override string Engine => "SQL Server";

    public override string LokiProvider => SqlServerProvider.Name;

    public override BenchmarkDbContext CreateDbContext() =>
        new SqlServerBenchmarkDbContext(new DbContextOptionsBuilder<SqlServerBenchmarkDbContext>().UseSqlServer(ConnectionString).Options);

    private const string TableExists = "SELECT COUNT(*) FROM sys.tables WHERE name = N'BenchmarkRows' AND SCHEMA_NAME(schema_id) = N'dbo'";

    private const string MissingTable =
        """The SQL Server database has no dbo.BenchmarkRows table but other tables, so EnsureCreated did not create it. Point ConnectionStrings:SqlServer at a database of its own (for example LokiSamplesBenchmark); see samples/README.md.""";

    public override async Task PrepareAsync(CancellationToken ct)
    {
        await using BenchmarkDbContext db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(ct);   // the benchmark's own database (LokiSamplesBenchmark), not the WebApi's
        // EnsureCreated does nothing when the database already has any table (for example the WebApi's LokiSamples database).
        if (Convert.ToInt64(await ExecuteAsync(TableExists, ct)) == 0)
        {
            throw new InvalidOperationException(MissingTable);
        }
    }

    protected override DbConnection CreateConnection() => new SqlConnection(ConnectionString);
}

internal sealed class OracleTarget(string connectionString) : BenchmarkTarget(connectionString)
{
    private const string TableExists = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'BENCHMARKROWS'";

    public override string Engine => "Oracle";

    public override string LokiProvider => OracleProvider.Name;

    public override BenchmarkDbContext CreateDbContext() =>
        new OracleBenchmarkDbContext(new DbContextOptionsBuilder<OracleBenchmarkDbContext>().UseOracle(ConnectionString).Options);

    public override async Task PrepareAsync(CancellationToken ct)
    {
        if (Convert.ToInt64(await ExecuteAsync(TableExists, ct)) == 0)
        {
            throw new InvalidOperationException(
                "The Oracle table BENCHMARKROWS does not exist: start the database with samples/docker-compose.yml, or run samples/db/oracle/init.sql.");
        }
    }

    protected override DbConnection CreateConnection() => new OracleConnection(ConnectionString);
}

internal static class BenchmarkTargets
{
    /// <summary>
    /// The targets for <paramref name="options"/>, with connection strings from configuration (never from the command line, where
    /// they would end up in the shell history). With "all", an engine without a connection string is skipped.
    /// </summary>
    public static IReadOnlyList<BenchmarkTarget> From(IConfiguration configuration, BenchmarkOptions options)
    {
        List<BenchmarkTarget> targets = [];
        foreach (string engine in options.Engines)
        {
            string? connectionString = configuration.GetConnectionString(engine == BenchmarkOptions.SqlServer ? "SqlServer" : "Oracle");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                targets.Add(engine == BenchmarkOptions.SqlServer ? new SqlServerTarget(connectionString) : new OracleTarget(connectionString));
            }
        }

        return targets.Count > 0
            ? targets
            : throw new InvalidOperationException("No connection string is configured for the selected engine(s); see samples/README.md.");
    }
}

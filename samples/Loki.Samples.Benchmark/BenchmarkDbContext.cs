using Microsoft.EntityFrameworkCore;

namespace Loki.Samples.Benchmark;

/// <summary>EF Core's side of the benchmark: default options (change tracking and EF Core's own batching).</summary>
internal abstract class BenchmarkDbContext(DbContextOptions options) : DbContext(options)
{
    public const string TableName = "BenchmarkRows";

    public DbSet<BenchmarkRow> BenchmarkRows => Set<BenchmarkRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<BenchmarkRow>(row =>
        {
            row.ToTable(Name(TableName));
            row.Property(r => r.Id).HasColumnName(Name(nameof(BenchmarkRow.Id)));
            row.Property(r => r.Name).HasColumnName(Name(nameof(BenchmarkRow.Name))).HasMaxLength(100);
            row.Property(r => r.Amount).HasColumnName(Name(nameof(BenchmarkRow.Amount))).HasPrecision(18, 2);
            row.Property(r => r.Quantity).HasColumnName(Name(nameof(BenchmarkRow.Quantity)));
            row.Property(r => r.CreatedOn).HasColumnName(Name(nameof(BenchmarkRow.CreatedOn)));
        });

    /// <summary>The name as the database stores it.</summary>
    protected abstract string Name(string name);
}

/// <summary>SQL Server; <c>EnsureCreated</c> creates the benchmark's own database and table on the first run.</summary>
internal sealed class SqlServerBenchmarkDbContext(DbContextOptions<SqlServerBenchmarkDbContext> options) : BenchmarkDbContext(options)
{
    protected override string Name(string name) => name;
}

/// <summary>
/// Oracle; the table comes from samples/db/oracle/init.sql. EF Core's Oracle provider quotes names exactly as written, and the DDL
/// uses unquoted (upper-case) names, so the model uses upper case. A separate context type keeps EF Core's model cache apart.
/// </summary>
internal sealed class OracleBenchmarkDbContext(DbContextOptions<OracleBenchmarkDbContext> options) : BenchmarkDbContext(options)
{
    protected override string Name(string name) => name.ToUpperInvariant();
}

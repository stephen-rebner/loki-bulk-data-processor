using System;
using System.Threading.Tasks;
using LokiBulkDataProcessor.IntegrationTests.EF;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LokiBulkDataProcessor.IntegrationTests.SqlServer;

/// <summary>
/// One SQL Server container for one test fixture. <see cref="ResetAsync"/> drops and re-creates the test database with
/// the EF Core migrations, so every test starts with empty tables and fresh identities.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public const string Password = "Pwd12345678!";

    /// <summary>The image Testcontainers.MsSql 4.x used by default, pinned because the parameterless builder is obsolete.</summary>
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private const string DatabaseName = "IntegrationTestsDb";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).WithPassword(Password).Build();

    private string _connectionString;

    /// <summary>The test database's connection string; available after <see cref="StartAsync"/>.</summary>
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("The container has not been started.");

    public async Task StartAsync()
    {
        await _container.StartAsync();
        _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
            TrustServerCertificate = true
        }.ToString();
    }

    public async Task ResetAsync()
    {
        await using TestDbContext context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    /// <summary>A new context on the test database; the caller disposes it.</summary>
    public TestDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>A new open connection to the test database; the caller disposes it.</summary>
    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

using System;
using System.Threading.Tasks;
using LokiBulkDataProcessor.IntegrationTests.Contract;
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
    /// <summary>
    /// The image Testcontainers.MsSql 4.x used by default (the parameterless builder is obsolete), pinned by tag and digest so a
    /// re-pushed tag fails instead of silently changing the database under the tests. The digest is the registry's manifest
    /// digest for the tag (resolved with <c>docker buildx imagetools inspect</c> in 9.0.0 step 9; linux/amd64 only).
    /// </summary>
    public const string Image =
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04@sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74";

    private const string DatabaseName = "IntegrationTestsDb";

    private readonly MsSqlContainer _container;

    public SqlServerTestDatabase()
    {
        Password = TestPasswords.NewPassword();
        _container = new MsSqlBuilder(Image).WithPassword(Password).Build();
    }

    /// <summary>The container's <c>sa</c> password: random for each fixture's database.</summary>
    public string Password { get; }

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

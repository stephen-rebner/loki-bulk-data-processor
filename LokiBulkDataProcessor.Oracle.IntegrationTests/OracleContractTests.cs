using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Runs the provider-neutral contract suite against Oracle with the default options (array bind, Unquoted identifiers, booleans as
/// NUMBER(1)): one container for the fixture, every table emptied and the identities restarted before each test. The contract's
/// <c>Posts</c> / <c>TestDbModels</c> destinations are folded to <c>"POSTS"</c> / <c>"TESTDBMODELS"</c> by Unquoted resolution.
/// </summary>
[TestFixture]
public sealed class OracleContractTests : BulkProcessorContractTests
{
    private readonly OracleTestDatabase _database = new();

    protected override string ProviderName => OracleProvider.Name;

    protected override string ConnectionString => _database.ConnectionString;

    protected override IReadOnlyCollection<string> ConnectionStringSecrets { get; } = new[] { OracleTestDatabase.Password };

    protected override void AddProvider(ILokiBulkDataProcessorBuilder builder) => builder.AddOracle();

    protected override Task StartDatabaseAsync() => _database.StartAsync();

    protected override Task StopDatabaseAsync() => _database.DisposeAsync().AsTask();

    protected override Task ResetDataAsync() => _database.ResetAsync();

    protected override async Task<DbConnection> OpenConnectionAsync() => await _database.OpenConnectionAsync();

    protected override Task<int> InsertBlogAsync(string url, DbTransaction transaction = null) =>
        _database.InsertBlogAsync(url, (OracleTransaction)transaction);

    protected override Task<int> CountBlogsAsync() => _database.CountBlogsAsync();

    protected override Task<IReadOnlyList<PostRow>> ReadPostsAsync() => _database.ReadPostsAsync();

    protected override Task<IReadOnlyList<TestDbModel>> ReadTestDbModelsAsync() => _database.ReadTestDbModelsAsync();

    protected override string WithPassword(string connectionString, string password) =>
        new OracleConnectionStringBuilder(connectionString) { Password = password, ConnectionTimeout = 5 }.ToString();

    /// <summary>Keeps the service name (EZConnect <c>host:port/service</c>) and replaces only the host and port.</summary>
    protected override string WithServer(string connectionString, string host, int port)
    {
        var builder = new OracleConnectionStringBuilder(connectionString);
        string serviceName = builder.DataSource[(builder.DataSource.IndexOf('/') + 1)..];
        builder.DataSource = $"{host}:{port}/{serviceName}";
        builder.ConnectionTimeout = 3;
        return builder.ToString();
    }

    protected override Task LockTestDbModelsTableAsync(DbTransaction transaction) =>
        OracleTestDatabase.ExecuteAsync((OracleTransaction)transaction, OracleSchema.LockTestDbModels);

    protected override string WithMaxPoolSize(string connectionString, int maxPoolSize) =>
        new OracleConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize, MinPoolSize = 0, ConnectionTimeout = 5 }.ToString();
}

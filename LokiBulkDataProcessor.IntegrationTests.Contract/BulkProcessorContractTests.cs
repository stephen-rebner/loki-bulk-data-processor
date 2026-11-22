using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Provider-neutral behaviour every <c>IBulkCopyProvider</c> must satisfy. A provider's integration test project derives
/// one sealed fixture from this class and implements the abstract members (database lifecycle, data reset, reading rows
/// back, opening a caller connection). One database per fixture (<see cref="StartDatabaseAsync"/> in
/// <c>[OneTimeSetUp]</c>); data is reset before every test (<see cref="ResetDataAsync"/> in <c>[SetUp]</c>).
/// </summary>
/// <remarks>
/// <para>The schema every provider must create: <c>Blogs(Id identity PK, Url)</c>, <c>Posts(Id identity PK, BlogId FK → Blogs,
/// Title, Content)</c> and <c>TestDbModels(Id identity PK, StringColumn, BoolColumn, DateColumn, NullableBoolColumn DEFAULT 1 (true),
/// NullableDateColumn)</c>. The DEFAULT lets the KeepNulls test tell an explicit null from a defaulted value.</para>
/// <para><b>The contract never writes identity columns.</b> No model mapping, DataTable, reader or JSON payload in it
/// carries Blogs.Id, Posts.Id or TestDbModels.Id; blogs are inserted through <see cref="InsertBlogAsync"/>, which returns the
/// generated Id. Ids are never compared and no assertion depends on row order, so the suite behaves the same whether a
/// provider's bulk copy ignores supplied identity values (SqlBulkCopy without KeepIdentity) or would insert them, and
/// whether the identity is <c>GENERATED ALWAYS</c> or <c>BY DEFAULT</c>. Failure tests therefore fail on the constraint they
/// name (for example the Posts → Blogs FOREIGN KEY), never on a primary key.</para>
/// <para>The tests in the partial files only use ADO.NET base types and assert only provider-neutral parts of messages.</para>
/// </remarks>
[NonParallelizable]
public abstract partial class BulkProcessorContractTests
{
    private ServiceProvider _services;
    private ServiceProvider _servicesWithoutMappings;

    /// <summary>The factory from a container with the contract mappings registered.</summary>
    protected IBulkProcessorFactory Factory { get; private set; }

    /// <summary>A new processor (contract mappings, own transaction) for each test.</summary>
    protected IBulkProcessor BulkProcessor { get; private set; }

    /// <summary>The provider name passed to <see cref="IBulkProcessorFactory.Create"/>.</summary>
    protected abstract string ProviderName { get; }

    /// <summary>A connection string to the fixture's database. Only valid after <see cref="StartDatabaseAsync"/>.</summary>
    protected abstract string ConnectionString { get; }

    /// <summary>Registers the provider under test, for example <c>builder.AddSqlServer()</c>.</summary>
    protected abstract void AddProvider(ILokiBulkDataProcessorBuilder builder);

    /// <summary>Starts the database (for example a container) and creates the schema. Runs once per fixture.</summary>
    protected abstract Task StartDatabaseAsync();

    /// <summary>Stops and disposes the database. Runs once per fixture, also when <see cref="StartDatabaseAsync"/> failed.</summary>
    protected abstract Task StopDatabaseAsync();

    /// <summary>Empties Blogs, Posts and TestDbModels (and restarts identities) so each test starts clean.</summary>
    protected abstract Task ResetDataAsync();

    /// <summary>Opens a new caller-owned connection to the database; the test disposes it.</summary>
    protected abstract Task<DbConnection> OpenConnectionAsync();

    /// <summary>Inserts a blog and returns its generated Id. With a <paramref name="transaction"/>, inserts inside it
    /// (on its connection); without one, inserts and commits on a connection of its own.</summary>
    protected abstract Task<int> InsertBlogAsync(string url, DbTransaction transaction = null);

    protected abstract Task<int> CountBlogsAsync();

    /// <summary>Every row of Posts, in any order (the contract never depends on row order).</summary>
    protected abstract Task<IReadOnlyList<PostRow>> ReadPostsAsync();

    /// <summary>Every row of TestDbModels, in any order (the contract never depends on row order).</summary>
    protected abstract Task<IReadOnlyList<TestDbModel>> ReadTestDbModelsAsync();

    /// <summary>
    /// The secret parts of <see cref="ConnectionString"/> (at least the password) that must never appear in a log entry, an exception
    /// message, an exception's <c>ToString()</c> or the processor's <c>ToString()</c>. Every value must be non-empty.
    /// </summary>
    protected abstract IReadOnlyCollection<string> ConnectionStringSecrets { get; }

    /// <summary>Returns <paramref name="connectionString"/> with its password replaced by <paramref name="password"/> (a login that fails).</summary>
    protected abstract string WithPassword(string connectionString, string password);

    /// <summary>
    /// Returns <paramref name="connectionString"/> pointing at <paramref name="host"/>:<paramref name="port"/>, where nothing listens,
    /// with a connect timeout of a few seconds so the failure is quick.
    /// </summary>
    protected abstract string WithServer(string connectionString, string host, int port);

    /// <summary>
    /// Takes an exclusive lock on TestDbModels inside <paramref name="transaction"/> (on its connection), so that any other session's
    /// INSERT into TestDbModels waits until the transaction ends. Used to make a save block, for the timeout and cancellation tests.
    /// </summary>
    protected abstract Task LockTestDbModelsTableAsync(DbTransaction transaction);

    /// <summary>Returns <paramref name="connectionString"/> with the pool limited to <paramref name="maxPoolSize"/>
    /// connections (and, ideally, a short connect timeout so a leak fails fast).</summary>
    protected abstract string WithMaxPoolSize(string connectionString, int maxPoolSize);

    /// <summary>The Posts destination table. <see cref="PostDataMapping"/> is keyed on <c>"Posts"</c>, so an override
    /// must still select that mapping (the destination-fallback tests depend on it).</summary>
    protected virtual string PostsTable => "Posts";

    /// <summary>The TestDbModels destination table (no mapping; columns map by name).</summary>
    protected virtual string TestDbModelsTable => "TestDbModels";

    /// <summary>A table that does not exist, used to make a copy fail.</summary>
    protected virtual string MissingTable => "NonExistentTable";

    [OneTimeSetUp]
    public async Task StartContractFixtureAsync()
    {
        await StartDatabaseAsync();
        _services = BuildServices(AddContractMappings);
        _servicesWithoutMappings = BuildServices(_ => { });
        Factory = _services.GetRequiredService<IBulkProcessorFactory>();
    }

    [OneTimeTearDown]
    public async Task StopContractFixtureAsync()
    {
        try
        {
            await DisposeServicesAsync();
        }
        finally
        {
            await StopDatabaseAsync();
        }
    }

    [SetUp]
    public async Task ResetContractTestAsync()
    {
        await ResetDataAsync();
        BulkProcessor = NewProcessor();
    }

    /// <summary>A new processor from the fixture's factory (contract mappings registered).</summary>
    protected IBulkProcessor NewProcessor(string connectionString = null) =>
        Factory.Create(ProviderName, connectionString ?? ConnectionString);

    /// <summary>A new processor from a container with no mappings registered, so every shape maps columns by name.</summary>
    protected IBulkProcessor NewProcessorWithoutMappings() =>
        _servicesWithoutMappings.GetRequiredService<IBulkProcessorFactory>().Create(ProviderName, ConnectionString);

    /// <summary>Builds a container with Loki and the provider under test registered; the caller disposes it.</summary>
    protected ServiceProvider BuildServices(Action<LokiBulkDataProcessorOptions> configure)
    {
        var services = new ServiceCollection();
        AddProvider(services.AddLokiBulkDataProcessor(configure));
        return services.BuildServiceProvider();
    }

    protected IBulkProcessor CreateProcessor(ServiceProvider services) =>
        services.GetRequiredService<IBulkProcessorFactory>().Create(ProviderName, ConnectionString);

    protected static void AddContractMappings(LokiBulkDataProcessorOptions options) =>
        options.AddMappingsFrom(typeof(PostMapping).Assembly);

    protected Task<int> GivenABlogAsync() => InsertBlogAsync("http://a-url.com");

    protected async Task ThePostsShouldBe(params (int BlogId, string Title, string Content)[] expected) =>
        (await ReadPostsAsync()).Select(post => (post.BlogId, post.Title, post.Content)).Should().BeEquivalentTo(expected);

    protected async Task ThePostTitlesShouldBe(params string[] expected) =>
        (await ReadPostsAsync()).Select(post => post.Title).Should().BeEquivalentTo(expected);

    protected async Task ThePostsTableShouldBeEmpty() => (await ReadPostsAsync()).Should().BeEmpty();

    protected async Task TheTestDbModelsShouldBe(IEnumerable<TestDbModel> expected) =>
        (await ReadTestDbModelsAsync()).Should().BeEquivalentTo(expected);

    /// <summary>Asserts the StringColumn values, in any order (the generated Id is never used as a sort key).</summary>
    protected async Task TheTestDbModelValuesShouldBe(params string[] expected) =>
        (await ReadTestDbModelsAsync()).Select(model => model.StringColumn).Should().BeEquivalentTo(expected);

    protected async Task TheTestDbModelsTableShouldBeEmpty() => (await ReadTestDbModelsAsync()).Should().BeEmpty();

    private async Task DisposeServicesAsync()
    {
        try
        {
            await DisposeIfBuiltAsync(_services);
        }
        finally
        {
            await DisposeIfBuiltAsync(_servicesWithoutMappings);
        }
    }

    private static async Task DisposeIfBuiltAsync(ServiceProvider services)
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }
    }
}

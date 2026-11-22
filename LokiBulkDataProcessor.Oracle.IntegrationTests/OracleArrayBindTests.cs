using System;
using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Oracle-specific behaviour of the array-bind writer on a real database (Oracle AI Database 26ai Free, the 23.26 release):
/// type bindings, <c>""</c> → NULL, triggers, constraint errors and their translated messages, identifier resolution in both
/// modes, timeouts and the caller's <c>OracleTransaction</c>. The provider-neutral behaviour runs in <see cref="OracleContractTests"/>.
/// One container for the fixture; every table is emptied before each test. No mappings are registered, so columns map by name.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed partial class OracleArrayBindTests
{
    /// <summary>Registered with <see cref="OracleIdentifierResolution.CatalogLookup"/>.</summary>
    private const string CatalogProvider = "OracleCatalog";

    /// <summary>Registered with <see cref="OracleBooleanBinding.Boolean"/>.</summary>
    private const string BooleanProvider = "OracleBoolean";

    private readonly OracleTestDatabase _database = new();
    private ServiceProvider _services;

    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        await _database.StartAsync();
        _services = BuildServices();
    }

    [OneTimeTearDown]
    public async Task StopDatabaseAsync()
    {
        try
        {
            await (_services?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    [SetUp]
    public Task ResetDataAsync() => _database.ResetAsync();

    /// <summary>A new processor for the default Oracle registration, or for <paramref name="providerName"/>.</summary>
    private IBulkProcessor Processor(string providerName = OracleProvider.Name) =>
        _services.GetRequiredService<IBulkProcessorFactory>().Create(providerName, _database.ConnectionString);

    /// <summary>An empty DataTable with typed columns; add rows with <c>Rows.Add(values)</c> (null cells as <see cref="DBNull.Value"/>).</summary>
    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach ((string name, Type type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(_ => { })
            .AddOracle()
            .AddOracle(options => options.IdentifierResolution = OracleIdentifierResolution.CatalogLookup, CatalogProvider)
            .AddOracle(options => options.BooleanBinding = OracleBooleanBinding.Boolean, BooleanProvider);
        return services.BuildServiceProvider();
    }
}

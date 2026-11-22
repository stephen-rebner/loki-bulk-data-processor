using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using Loki.BulkDataProcessor.Oracle;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// <see cref="OracleWriteMode.DirectPath"/> (<c>OracleBulkCopy</c>) on a real database (Oracle AI Database 26ai Free, the 23.26 release):
/// every input shape, Loki's and the caller's transaction, the documented hazards (one load per transaction, triggers refused, FKs not
/// enforced, duplicate keys fail the save so it is rolled back) with their translated messages, what a failed load leaves behind, cancellation and
/// identifier resolution. Direct path does not run the contract suite: several of its caller-transaction scenarios load more than once per
/// transaction, which direct path refuses by design (ORA-39822). One container for the fixture; every table is emptied before each test
/// (TRUNCATE also makes an UNUSABLE <c>KeyedRows</c> index VALID again). The contract mappings are registered.
/// </summary>
[TestFixture]
[NonParallelizable]
[Timeout(180_000)]   // per test: a direct-path load or a reader stuck behind a lock fails the test instead of hanging CI
public sealed partial class OracleDirectPathTests
{
    /// <summary>Registered with <see cref="OracleWriteMode.DirectPath"/> and <see cref="OracleIdentifierResolution.CatalogLookup"/>.</summary>
    private const string CatalogProvider = "OracleDirectPathCatalog";

    /// <summary>The default <see cref="OracleWriteMode.ArrayBind"/> registration, for the conventional INSERT after a direct-path load.</summary>
    private const string ArrayBindProvider = "OracleArrayBind";

    private const string TestDbModelsTable = "TestDbModels";

    private const string CountTestDbModels = "SELECT COUNT(*) FROM TestDbModels";

    private const string InsertTestDbModel =
        "INSERT INTO TestDbModels (StringColumn, BoolColumn, DateColumn) VALUES (:stringValue, 1, :dateValue)";

    private const string SelectTestDbModelStrings = "SELECT StringColumn FROM TestDbModels";

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

    /// <summary>A new processor for the DirectPath registration, or for <paramref name="providerName"/>.</summary>
    private IBulkProcessor Processor(string providerName = OracleProvider.Name) =>
        _services.GetRequiredService<IBulkProcessorFactory>().Create(providerName, _database.ConnectionString);

    /// <summary>A processor that joins <paramref name="transaction"/>.</summary>
    private IBulkProcessor Processor(OracleTransaction transaction)
    {
        IBulkProcessor processor = Processor();
        processor.Transaction = transaction;
        return processor;
    }

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

    private static async Task<int> CountTestDbModelsAsync(OracleConnection connection) =>
        (await OracleTestDatabase.QueryAsync(connection, CountTestDbModels, reader => Convert.ToInt32(reader.GetValue(0))))[0];

    /// <summary>Counts TestDbModels on <paramref name="connection"/> with a CommandTimeout, so the query cannot wait forever.</summary>
    private static async Task<int> CountTestDbModelsAsync(OracleConnection connection, int commandTimeoutSeconds)
    {
        await using OracleCommand command = connection.CreateCommand();
        command.CommandText = CountTestDbModels;
        command.CommandTimeout = commandTimeoutSeconds;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private Task<IReadOnlyList<string>> ReadTestDbModelStringsAsync() =>
        _database.QueryAsync(SelectTestDbModelStrings, reader => reader.GetString(0));

    private static Task InsertTestDbModelAsync(OracleTransaction transaction, string value) =>
        OracleTestDatabase.ExecuteAsync(transaction, InsertTestDbModel,
            OracleTestDatabase.Bind("stringValue", value), OracleTestDatabase.Bind("dateValue", new DateTime(2026, 1, 1)));

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(PostMapping).Assembly))
            .AddOracle(options => options.WriteMode = OracleWriteMode.DirectPath)
            .AddOracle(options =>
            {
                options.WriteMode = OracleWriteMode.DirectPath;
                options.IdentifierResolution = OracleIdentifierResolution.CatalogLookup;
            }, CatalogProvider)
            .AddOracle(name: ArrayBindProvider);
        return services.BuildServiceProvider();
    }
}

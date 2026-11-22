using System.Data;
using Loki.BulkDataProcessor;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Imports;
using Loki.Samples.WebApi.Mappings;

namespace Loki.Samples.WebApi.Services;

/// <summary>
/// The four input shapes Loki saves (models, DataTable, IDataReader, JSON), each on the engine the request chose. Every save runs
/// in Loki's own transaction: it commits on success and rolls back on failure, so a failed request leaves nothing behind.
/// </summary>
internal sealed class BulkImportService(EngineCatalog engines)
{
    /// <summary>Models: <see cref="CustomerImportRowMapping"/> maps them (FullName → Name, ...).</summary>
    public async Task<ImportResult> SaveCustomersAsync(DatabaseEngine engine, IReadOnlyList<CustomerRequest>? customers, CancellationToken ct)
    {
        List<CustomerImportRow> rows = ToImportRows(customers);
        IBulkProcessor processor = engines.CreateProcessor(engine);
        await processor.SaveAsync(rows, ImportTables.Customers, ct);
        return new ImportResult(processor.ProviderName, ImportTables.Customers, rows.Count);
    }

    /// <summary>DataTable: its TableName "ProductImport" selects <see cref="ProductImportMapping"/>.</summary>
    public async Task<ImportResult> SaveProductsFromDataTableAsync(DatabaseEngine engine, int count, CancellationToken ct)
    {
        using DataTable rows = ProductImportRows.Create(RequestGuards.ThrowIfNotARowCount(count, nameof(count)));
        IBulkProcessor processor = engines.CreateProcessor(engine);
        processor.BatchSize = 2_000;   // per processor only; SQL Server: rows per batch, Oracle: rows per all-or-nothing chunk
        await processor.SaveAsync(rows, ImportTables.Products, ct);
        return new ImportResult(processor.ProviderName, ImportTables.Products, rows.Rows.Count);
    }

    /// <summary>
    /// IDataReader: any reader works (a SqlDataReader or OracleDataReader over another database, a CSV reader, ...). A reader has no
    /// TableName, so the mapping key is passed as <c>sourceName</c>. Loki never disposes a caller's reader: this method does.
    /// </summary>
    public async Task<ImportResult> SaveProductsFromReaderAsync(DatabaseEngine engine, int count, CancellationToken ct)
    {
        using DataTable source = ProductImportRows.Create(RequestGuards.ThrowIfNotARowCount(count, nameof(count)));
        using IDataReader reader = source.CreateDataReader();
        IBulkProcessor processor = engines.CreateProcessor(engine);
        await processor.SaveAsync(reader, ImportTables.Products, ProductImportMapping.Source, ct);
        return new ImportResult(processor.ProviderName, ImportTables.Products, source.Rows.Count);
    }

    /// <summary>
    /// JSON: <c>SaveAsync(stream, destinationTableName)</c> with a table chosen by code from the allow-list. The payload's own
    /// <c>tableName</c> is validated but ignored, and the JSON mapping of that table limits the columns. Never
    /// <c>SaveAsync(stream)</c> for a payload from a caller: there the payload would pick the table. Loki never disposes the
    /// stream (ASP.NET Core owns the request body).
    /// </summary>
    public async Task<ImportResult> SaveJsonAsync(DatabaseEngine engine, string tableRouteValue, Stream json, CancellationToken ct)
    {
        string table = ImportTables.JsonDestinationFor(tableRouteValue);
        IBulkProcessor processor = engines.CreateProcessor(engine);
        await processor.SaveAsync(json, table, ct);
        return new ImportResult(processor.ProviderName, table, Rows: null);   // the payload is parsed by Loki; it reports no count
    }

    private static List<CustomerImportRow> ToImportRows(IReadOnlyList<CustomerRequest>? customers)
    {
        RequestGuards.ThrowIfNotARowCount(customers?.Count ?? 0, "The number of customers");
        DateTime now = DateTime.UtcNow;
        return customers!.Select(customer => new CustomerImportRow
        {
            FullName = RequestGuards.ThrowIfBlankOrLonger(customer?.Name, 200, "name"),
            EmailAddress = RequestGuards.ThrowIfLonger(customer?.Email, 320, "email"),
            RegisteredOn = now
        }).ToList();
    }
}

# Loki Bulk Data Processor

The Loki Bulk Data Processor is a fast, efficient .NET library for bulk data inserts into SQL Server using `SqlBulkCopy`. Designed for ASP.NET Core, it supports:

- `IEnumerable<T>` models
- `DataTable`
- `IDataReader` (v6.0.0+)
- `JSON streams` (v6.0.0+)
- `Structured logging and mapping support` (v6.0.0+)

Perfect for high-throughput ETL pipelines and real-time ingestion scenarios.

---

This project has been upgraded to target `.NET 10`.

## Install

```bash
  dotnet add package LokiBulkDataProcessor
```

## Providers

From 9.0.0, Loki supports more than one database engine. Each engine is a **provider**: a named implementation of
`IBulkCopyProvider` that you register at startup. The `LokiBulkDataProcessor` package includes the SQL Server provider
(`SqlBulkCopy` on `Microsoft.Data.SqlClient`). An Oracle provider (`LokiBulkDataProcessor.Oracle`) is planned for 9.0.0.

### 1. Register Loki and one or more providers

```csharp
services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(Startup).Assembly))
        .AddSqlServer();                  // registered under the name "SqlServer" (SqlServerProvider.Name)
     // .AddSqlServer("Reporting")        // the same engine again, under another name
```

`AddLokiBulkDataProcessor` registers the shared services: your mappings, logging, JSON options and a singleton
`IBulkProcessorFactory`. Each `AddXxx()` call adds one provider under a name. You can call `AddLokiBulkDataProcessor`
only once per service collection, so chain every provider on the builder it returns.

The single-database registration from 8.x still works. It registers the SQL Server provider for you, plus a scoped
`IBulkProcessor` for that connection string:

```csharp
services.AddLokiBulkDataProcessor(connectionString, typeof(Startup).Assembly);
// inject IBulkProcessor as before
```

### 2. Create a processor for a provider and a database

```csharp
public class ImportService(IBulkProcessorFactory factory)
{
    public async Task ImportAsync(IEnumerable<Order> orders, string connectionString, CancellationToken ct)
    {
        IBulkProcessor processor = factory.Create(SqlServerProvider.Name, connectionString);
        processor.BatchSize = 5_000;
        await processor.SaveAsync(orders, "dbo.Orders", ct);
    }
}
```

- `Create` looks up the provider by name (case-insensitive) and returns a **new** processor bound to that provider and
  connection string. `processor.ProviderName` tells you which provider it uses.
- An unknown name throws `ArgumentException`. Use `factory.ProviderNames` or `factory.HasProvider(name)` to check first.
- Settings (`BatchSize`, `Timeout`, `Transaction`) belong to that processor only, and a processor is not thread-safe.
  Create one per unit of work. To write to two databases, create two processors, for example
  `factory.Create("SqlServer", csA)` and `factory.Create("Reporting", csB)`.
- A transaction you assign is checked against the provider straight away. For example, an Oracle transaction on a
  SQL Server processor throws `ArgumentException` before any database work.

### 3. How a save reaches the right database

You never choose a provider-specific save method. Every `SaveAsync` overload (models, `DataTable`, `IDataReader`, JSON)
takes the same path, and the provider is called only at the points where the database engine matters:

```
factory.Create("SqlServer", cs)        → processor bound to the SQL Server provider
processor.SaveAsync(anything)
  → your input becomes an IDataReader + column maps     (mappings resolved here; no provider code)
  → shared pipeline, using the processor's provider:
       open a connection + transaction, or join yours    provider.CreateConnection / AdoptExternalTransaction
       write the rows                                    provider.CreateWriter(...).WriteAsync(...)
                                                          (SQL Server: SqlBulkCopy.WriteToServerAsync)
       on failure, wrap the driver error                  provider.TranslateException (driver error kept as InnerException)
       commit / roll back / dispose                       Loki itself, only for a transaction Loki opened
```

Committing, rolling back and disposing never happen in the provider, so the transaction rules are the same for every
database. If you set `processor.Transaction`, Loki never commits it, rolls it back, disposes it or closes its connection,
even when the save fails. Otherwise Loki opens its own transaction, commits it on success, rolls it back on failure, and
always disposes it.

### Writing your own provider

The provider interfaces are public, in the `Loki.BulkDataProcessor.Providers` namespace of `LokiBulkDataProcessor.Core`:

- Implement `IBulkCopyProvider`: `Name`, `CreateConnection`, `AdoptExternalTransaction`, `CreateWriter`, `TranslateException`.
- Implement `IBulkWriter`, which has a single `WriteAsync`.
- Register them with a builder extension, for example
  `public static ILokiBulkDataProcessorBuilder AddMyDb(this ILokiBulkDataProcessorBuilder builder, string name = "MyDb")`,
  that adds your provider as a singleton `IBulkCopyProvider`.

A provider must be stateless and thread-safe, because one instance serves every processor. It must never commit, roll
back or dispose the connection or transaction it is given, and `TranslateException` must return a **new** exception whose
`InnerException` is the original.

## Performance

Benchmark: 100,000 records inserted into an empty SQL Server table with 4 columns (1 primary key, 1 foreign key), on a 2.7GHz i7, 16 GB RAM, SSD.

| Method                | Time Taken    | Memory Usage |
|-----------------------|---------------|--------------|
| Loki Bulk Processor   | 2.99 seconds  | 266 MB       |
| Entity Framework Core | 68.74 seconds | 1 GB+        |

Inserted 100,000 records into an empty table with 1 PK and 1 FK column.

> ⚠️ Entity Framework was used with `AddAsync` and `SaveChangesAsync`. Loki used a single bulk insert call.

## Upgrade Guide

| From Version | What Changed                                      | Action Required         |
|--------------|--------------------------------------------------|--------------------------|
| ≤ 6.0.0      | Web API execution failed after the first request | **Upgrade to v7.0.0+** ✅ |
| 6.0.0        | `DataTableMapping` renamed to `DataMapping`      | **Update references** ⚠️ |
| 7.0.1        | Project upgraded to .NET 10                       | **No action required unless you target an earlier runtime** ✅ |
| 8.x          | 9.0.0: driver is now `Microsoft.Data.SqlClient` 6.1.8 (was the deprecated `System.Data.SqlClient`), so EF Core transactions (`GetDbTransaction()`) are accepted. `Microsoft.Data.SqlClient` defaults to `Encrypt=true`. A `System.Data.SqlClient` transaction now throws `ArgumentException` | **Change `using System.Data.SqlClient` to `using Microsoft.Data.SqlClient` wherever you create connections/transactions for Loki. For dev servers with self-signed certificates add `TrustServerCertificate=true` to the connection string; do not turn encryption off** ⚠️ |
| 8.x          | 9.0.0: the mapping API (`ModelMapping<T>`, `DataMapping`, …), `IBulkProcessor` and `LokiLoggingOptions` moved to the new `LokiBulkDataProcessor.Core` package with **unchanged namespaces**. `LokiBulkDataProcessor` depends on it, so it comes in transitively, and type forwarders let assemblies built against 8.x (e.g. a separate mappings library) keep binding | **No action required for source code** ✅ |
| 8.x          | 9.0.0: the empty public class `Loki.BulkDataProcessor.Properties.AssemblyInfo` is removed, and the assembly version is now generated (`9.0.0.0`, was a hard-coded `1.0.0.02`) | **Remove any reference to `AssemblyInfo`: the `Loki.BulkDataProcessor.Properties` namespace no longer exists, so `using Loki.BulkDataProcessor.Properties;` no longer compiles** ⚠️ |
| 8.x          | 9.0.0: models with get-only (e.g. computed `=>`) or set-only properties no longer throw `NullReferenceException`; those properties are skipped and **not saved**. A destination column matched only by a get-only property is left NULL (#19) | **Give any property you need persisted a public getter and setter** ⚠️ |

## Documentation

Everything you need to get started:

- 📝 [Full Documentation (Wiki)](https://github.com/stephen-rebner/loki-bulk-data-processor/wiki)
- 📺 [YouTube Tutorial – Part 1](https://youtu.be/vci6kQWcmhs) — Introduction & Setup
- 📺 [YouTube Tutorial – Part 2](https://youtu.be/2ABIrl_nwMk) — Advanced Usage & Mapping
- 🧪 [Example Projects on GitHub](https://github.com/stephen-rebner/loki-bulk-data-processor-examples)

## Support

If this library saved you time or made your life easier, consider showing your support:

- ☕ [Buy me a coffee](https://www.paypal.com/paypalme2/StephenRebner?locale.x=en_GB)
- ⭐ Star the [GitHub repo](https://github.com/stephen-rebner/loki-bulk-data-processor) to help others discover it
- 🗣️ Share your experience or contribute feedback via [issues](https://github.com/stephen-rebner/loki-bulk-data-processor/issues)

Every bit helps keep the project going.



# Loki Bulk Data Processor

The Loki Bulk Data Processor is a fast, efficient .NET library for bulk data inserts into SQL Server using `SqlBulkCopy`. Designed for ASP.NET Core, it supports:

- `IEnumerable<T>` models
- `DataTable`
- `IDataReader` (v6.0.0+)
- `JSON streams` (v6.0.0+)
- `Structured logging and mapping support` (v6.0.0+)

Perfect for high-throughput ETL pipelines and real-time ingestion scenarios.

---

## Install

```bash
  dotnet add package LokiBulkDataProcessor
```

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


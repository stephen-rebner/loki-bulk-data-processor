# Loki Bulk Data Processor

The Loki Bulk Data Processor is a fast .NET library for bulk inserts. You hand it `IEnumerable<T>` models, a `DataTable`,
an `IDataReader` or a JSON stream, and it streams the rows into a destination table with the database's native bulk-copy
engine:

- **SQL Server** with `SqlBulkCopy` (`Microsoft.Data.SqlClient`)
- **Oracle** (19c and later) with an array-bound `INSERT`, or opt-in `OracleBulkCopy` direct path for staging tables

It targets `.NET 10`, fits ASP.NET Core dependency injection, and is built for ETL pipelines and high-throughput ingestion.

This README is the documentation for 9.x. Upgrading from 8.x? See the [Upgrade guide](#upgrade-guide).

## Contents

- [Install](#install)
- [Register Loki](#register-loki)
- [Choosing the database at run time (IBulkProcessorFactory)](#choosing-the-database-at-run-time-ibulkprocessorfactory)
- [Mappings](#mappings)
- [Saving data](#saving-data)
- [JSON](#json)
- [Transactions](#transactions)
- [BatchSize, Timeout and cancellation](#batchsize-timeout-and-cancellation)
- [Errors](#errors)
- [Logging](#logging)
- [SQL Server](#sql-server)
- [Oracle](#oracle)
- [Oracle direct path](#oracle-direct-path)
- [Writing your own provider](#writing-your-own-provider)
- [Performance](#performance)
- [Upgrade guide](#upgrade-guide)
- [Documentation](#documentation)
- [Support](#support)

## Install

Loki ships as three packages with the same version. Reference the provider package for each database you write to. Both
providers depend on `LokiBulkDataProcessor.Core`, so you never need to reference Core yourself unless you write your own
provider.

| Package | What it is | Driver |
|---------|------------|--------|
| `LokiBulkDataProcessor` | The SQL Server provider (same package id as 8.x) | `Microsoft.Data.SqlClient` 6.1.8 |
| `LokiBulkDataProcessor.Oracle` | The Oracle provider (Oracle Database 19c and later) | `Oracle.ManagedDataAccess.Core` 23.26.400 |
| `LokiBulkDataProcessor.Core` | Provider-neutral core: `IBulkProcessor`, `IBulkProcessorFactory`, the mapping API, the JSON reader, the provider SPI. Comes in with either provider. | none |

```bash
dotnet add package LokiBulkDataProcessor          # SQL Server
dotnet add package LokiBulkDataProcessor.Oracle   # Oracle
```

## Register Loki

```csharp
using Loki.BulkDataProcessor.DependancyInjection;   // (sic) the namespace is spelt this way
using Loki.BulkDataProcessor.Oracle;

services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(Program).Assembly))
        .AddSqlServer()                          // registered as "SqlServer" (SqlServerProvider.Name)
        .AddOracle();                            // registered as "Oracle" (OracleProvider.Name), array bind
```

`AddLokiBulkDataProcessor` registers your mappings, logging, the JSON options and a singleton `IBulkProcessorFactory`.
Each `AddXxx()` call adds one **provider** under a name. You can register the same engine more than once with different
settings by giving it another name, for example `.AddSqlServer("Reporting")` or
`.AddOracle(o => o.IdentifierResolution = OracleIdentifierResolution.CatalogLookup, name: "Oracle.Catalog")`.

`AddLokiBulkDataProcessor` may be called **once** per service collection (a second call throws
`InvalidOperationException`), so chain every provider on the builder it returns.

### Options

| Option | Default | What it does |
|--------|---------|--------------|
| `AddMappingsFrom(assembly)` | none | Registers every `ModelMapping<T>` and `DataMapping` in the assembly. Call it once per assembly. Mappings are created and validated here: an invalid or duplicate mapping fails at startup. |
| `UseHostLoggerFactory` | `true` | Log through the host's `ILoggerFactory` from DI. With no `ILoggerFactory` registered, Loki logs nothing. Set `false` to keep Loki silent. |
| `LoggerFactory` | `null` | A logger factory for Loki only; it wins over the host's. Loki never registers or disposes it. |
| `UseConsoleLogging(...)` | off | **Obsolete** (removed in 10.0): Loki-private console logging. Configure console output in your host's logging instead. |
| `RequireDataMappingForJson` | `false` | Every JSON save must find a `DataMapping`; otherwise it throws `InvalidOperationException` before connecting, so a payload cannot choose the columns. |
| `MaxJsonPayloadBytes` | `null` (no limit) | The largest JSON payload read. A larger one throws `InvalidDataException` as soon as the limit is passed. Set it for payloads from untrusted callers: a JSON payload is buffered in memory. |

### The 8.x single-database registration

The 8.x registration still works. It registers the SQL Server provider plus a **scoped** `IBulkProcessor` for one
connection string, so code that injects `IBulkProcessor` keeps working:

```csharp
services.AddLokiBulkDataProcessor(connectionString, typeof(Program).Assembly);
// optional: loggerFactory: myLoggerFactory
```

Each DI scope gets its own processor and settings. Do not capture the scoped `IBulkProcessor` in a singleton; long-lived
services should inject `IBulkProcessorFactory` instead. The JSON options above are available only through the builder
overload, and because `AddLokiBulkDataProcessor` may be called only once, you cannot combine this overload with
`.AddOracle()`: use the builder and register your own scoped `IBulkProcessor` from the factory if you need both.

## Choosing the database at run time (IBulkProcessorFactory)

Inject `IBulkProcessorFactory` and create a processor per unit of work:

```csharp
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.SqlServer;

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

- `Create(providerName, connectionString)` finds the provider by name (case-insensitive) and returns a **new** processor
  bound to that provider and connection string. `processor.ProviderName` tells you which one it uses.
- An unknown name throws `ArgumentException`. Check first with `factory.HasProvider(name)` or `factory.ProviderNames`.
- `BatchSize`, `Timeout` and `Transaction` belong to that processor only. A processor is cheap and **not thread-safe**: use
  one per unit of work. To write to two databases, create two processors.
- A transaction you assign is checked against the provider straight away: an Oracle transaction on a SQL Server processor
  throws `ArgumentException` before any database work.
- There is no public getter for the connection string, and Loki never logs it or puts it in an exception message.

Because the provider is chosen by a string, an application that lets users pick the engine can map its own enum to a
provider name. This sample (and its unit test, with a Moq double of the factory) is compiled and run by the library's
test suite:

```csharp
services.AddLokiBulkDataProcessor()
        .AddSqlServer()                                // "SqlServer"
        .AddOracle();                                  // "Oracle": array bind (the default)
services.AddScoped<SeedWriter>();
```

```csharp
public enum DatabaseEngine { SqlServer, Oracle, PostgreSql }

public sealed class SeedWriter(IBulkProcessorFactory bulkProcessorFactory)
{
    public async Task WriteAsync(DatabaseEngine engine, string connectionString, DataTable rows, CancellationToken ct)
    {
        string provider = engine.ToString();                  // "SqlServer", "Oracle", "PostgreSql"
        if (!bulkProcessorFactory.HasProvider(provider))
        {
            throw new NotSupportedException($"Bulk seeding is not supported for {provider}.");
        }

        IBulkProcessor processor = bulkProcessorFactory.Create(provider, connectionString);
        processor.BatchSize = 2000;
        await processor.SaveAsync(rows, rows.TableName, ct);
    }
}

public class SeedWriterTests
{
    [Test]
    public async Task WriteAsync_ShouldSaveThroughTheFactoryProcessor_WhenTheEngineIsSqlServer()
    {
        Mock<IBulkProcessor> processor = new();
        Mock<IBulkProcessorFactory> factory = new();
        factory.Setup(f => f.HasProvider("SqlServer")).Returns(true);
        factory.Setup(f => f.Create("SqlServer", "cs")).Returns(processor.Object);
        DataTable rows = new("Customers");

        await new SeedWriter(factory.Object).WriteAsync(DatabaseEngine.SqlServer, "cs", rows, CancellationToken.None);

        processor.VerifySet(p => p.BatchSize = 2000);
        processor.Verify(p => p.SaveAsync(rows, "Customers", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

If the destination table comes from your users (as it does for a seeding tool), decide in your code which tables they may
write to: Loki checks that a name is a valid identifier, not that the caller is allowed to write to it.

## Mappings

Without a mapping, columns map 1:1 by name: every public property with a public getter **and** setter (models), or every
source column (DataTable, reader, JSON). A mapping renames columns and limits the copy to the mapped columns.

```csharp
using Loki.BulkDataProcessor.Mappings;

public class PostMapping : ModelMapping<Post>
{
    public PostMapping()
    {
        Map(post => post.BlogId).ToDestinationColumn("BlogId");
        Map(post => post.Title).ToDestinationColumn("Title");
        Map(post => post.Body).ToDestinationColumn("Content");
    }
}

public class PostImportMapping : DataMapping
{
    public override string SourceTableName => "PostImport";

    public PostImportMapping()
    {
        Map("ImportTitle").ToDestinationColumn("Title");
        Map("ImportBody").ToDestinationColumn("Content");
        Map("ImportBlogId").ToDestinationColumn("BlogId");
    }
}
```

Register the assembly with `options.AddMappingsFrom(assembly)`. Rules:

- A `ModelMapping<T>` applies to saves of `IEnumerable<T>`.
- A `DataMapping` applies when its `SourceTableName` equals the input's **source name** (ordinal, case-sensitive). The source
  name is, in order:
  1. the `sourceName` argument of `SaveAsync(IDataReader, destinationTableName, sourceName)`;
  2. otherwise `DataTable.TableName`, if it is not blank;
  3. otherwise the **destination table name** (also for JSON and for readers saved without a `sourceName`).
- Two mappings for the same model type or the same `SourceTableName` fail at startup with `InvalidOperationException`.
  So does a mapping with a duplicate or empty column.
- Properties without a public getter and setter (for example computed `=>` properties) are never copied. Mapping one
  explicitly throws `ArgumentException` before any connection is opened.
- Destination column names are passed to the provider as written. SQL Server matches them case-sensitively in
  `SqlBulkCopy` mappings; Oracle matches them as described under [Identifiers and casing](#identifiers-and-casing).

## Saving data

Every overload takes an optional `CancellationToken` as its last parameter, writes in Loki's own transaction (or yours),
and goes through the same pipeline for every provider.

```csharp
await processor.SaveAsync(posts, "Posts", ct);                    // IEnumerable<T> (ModelMapping<T> or by name)
await processor.SaveAsync(dataTable, "Posts", ct);                // DataTable (mapping key: TableName, else "Posts")
await processor.SaveAsync(reader, "Posts", ct);                   // IDataReader (mapping key: "Posts")
await processor.SaveAsync(reader, "Posts", "PostImport", ct);     // IDataReader with an explicit mapping key
await processor.SaveAsync(jsonStream, ct);                        // JSON: the payload's tableName is the destination
await processor.SaveAsync(jsonStream, "Posts", ct);               // JSON: your code chooses the destination
```

- Argument errors throw **synchronously** (before a `Task` is returned): a null or empty collection, a `DataTable` with
  no rows, a null reader or stream, or a blank table name (`ArgumentException` / `ArgumentNullException`).
- Loki disposes the readers it creates; it never disposes your `IDataReader` or `Stream`.
- A `DataTable` row in the `Deleted` state is skipped (the `DataTableReader` behaviour).
- `Timeout` must not be negative and `BatchSize` must not be negative (`ArgumentException` when set).

## JSON

The JSON overloads read a document with a `tableName`, a typed `columns` list and `data` rows:

```json
{
  "tableName": "Posts",
  "columns": [
    { "name": "Title", "type": "string" },
    { "name": "BlogId", "type": "int" },
    { "name": "PublishedOn", "type": "datetime" }
  ],
  "data": [
    { "Title": "Hello", "BlogId": 1, "PublishedOn": "2026-11-22T10:00:00Z" },
    { "Title": "Draft", "BlogId": 1, "PublishedOn": null }
  ]
}
```

- Column types: `string`, `char`, `int`/`int32`, `long`/`int64`, `short`/`int16`, `byte`, `decimal`, `double`,
  `float`/`single`, `bool`/`boolean`, `datetime`/`date`, `guid`. Any other type is rejected with `InvalidDataException`,
  as is a payload without `tableName`, `columns` or `data`, or with duplicate column names.
- JSON `null` is saved as NULL. Booleans must be JSON `true`/`false` or the strings `"true"`/`"false"` (any case); `1`,
  `0` or `"yes"` throw. Dates should be ISO 8601; other formats are read with the invariant culture (`03/04/2026` is
  4 March on every server).
- A value that cannot be converted to its column's type throws a `FormatException` that names only the column and type,
  never the value.
- The payload is parsed with a maximum depth of 64, without comments or trailing commas, and buffered in memory in full:
  set `MaxJsonPayloadBytes` for payloads you do not control.

**Security: the payload must not choose the table.** `SaveAsync(stream)` writes to the payload's `tableName` and uses the
`DataMapping` keyed on it, so whoever writes the payload decides which table it lands in. Use it only for payloads you
trust. For anything else, use `SaveAsync(stream, destinationTableName)`: the payload's `tableName` is still validated but
ignored, the table and the mapping key are the ones your code passes, and with `RequireDataMappingForJson = true` the
payload cannot add columns your mapping does not list. Callers decide which tables a payload may write to; Loki does not.

## Transactions

By default, every save opens its own connection and transaction, commits on success, rolls back on failure, and
disposes both on every path.

To write inside your own transaction, set `processor.Transaction`. Loki then **never commits, rolls back, disposes or
closes** your transaction or its connection, also when the save fails. You decide:

```csharp
using Microsoft.Data.SqlClient;

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync(ct);
await using SqlTransaction transaction = connection.BeginTransaction();

IBulkProcessor processor = factory.Create(SqlServerProvider.Name, connectionString);
processor.Transaction = transaction;
try
{
    await processor.SaveAsync(orders, "dbo.Orders", ct);
    await processor.SaveAsync(orderLines, "dbo.OrderLines", ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(CancellationToken.None);   // Loki left it to you
    throw;
}
```

- With EF Core, pass `dbContext.Database.CurrentTransaction.GetDbTransaction()` (or the result of
  `BeginTransactionAsync()`) to `processor.Transaction`.
- The transaction must belong to the processor's driver: `Microsoft.Data.SqlClient.SqlTransaction` for SQL Server,
  `Oracle.ManagedDataAccess.Client.OracleTransaction` for Oracle. Anything else throws `ArgumentException` when you set it.
  `processor.Transaction = null` clears it.
- **Roll back after a failed save.** Some server errors doom the transaction (for example a SQL Server error with
  `XACT_ABORT ON`, or a severe error), and a failed Oracle save can leave its earlier chunks in your transaction (see
  [Oracle](#oracle)). Do not assume you can keep writing in a transaction after a save failed.
- A transaction that was already committed or rolled back throws `InvalidOperationException`
  ("The supplied transaction has already been committed or rolled back.").

## BatchSize, Timeout and cancellation

| | SQL Server | Oracle array bind (default) | Oracle direct path |
|---|---|---|---|
| `BatchSize` (default 5000) | Rows per `SqlBulkCopy` batch; 0 = one batch | Rows per chunk (one array-bound `INSERT`); 0 = 5000, at most 20,000 | Rows per batch sent to the server; 0 = 5000 |
| `Timeout` in seconds (default 30; 0 = no limit) | `SqlBulkCopy.BulkCopyTimeout` for the whole copy | Per chunk (and per catalogue query) | Passed to the driver, but **not** a reliable upper bound |
| Atomicity | All batches in one transaction | Each chunk all-or-nothing; all chunks in one transaction | One load, all-or-nothing |
| Cancellation | Cancels the running copy | Checked before each chunk and sent to the running chunk | Checked only before the load starts |

- A cancelled save throws `OperationCanceledException` (the driver's error, if any, is the `InnerException`). Loki's own
  transaction is rolled back; yours is left to you.
- An expired `Timeout` throws `InvalidOperationException` with the driver's error as `InnerException`.
- With Loki's own transaction a failed or cancelled save leaves nothing behind, whatever the BatchSize.

## Errors

Loki's exception messages are fixed and **data-free**: they name the destination table (sanitised) and, when there is
one, the database error code, but never a row value, a driver message or the connection string. The driver's exception
is always the `InnerException`, so you can still inspect it, for example for retry decisions:

```csharp
catch (InvalidOperationException ex) when (ex.InnerException is SqlException { Number: 1205 })   // deadlock victim
{
    // retry
}
```

| Exception | When |
|-----------|------|
| `ArgumentException` / `ArgumentNullException` | Bad arguments (thrown synchronously), an unknown provider name, a transaction of another driver. Oracle: an invalid table name (see [Oracle](#oracle)). |
| `InvalidOperationException` | The copy failed: `Bulk copy to 'dbo.Posts' failed (SQL Server error 547). See InnerException for details.` or `... failed (Oracle error ORA-02291) ...`. A destination column that does not exist: `The given column mappings does not match up with any column in the destination table.` Also: the connection could not be opened (`Could not open a connection or begin a transaction for provider 'SqlServer'. See InnerException for details.`), registration errors, and `RequireDataMappingForJson` with no mapping. |
| `OperationCanceledException` | The token was cancelled. |
| `InvalidDataException`, `JsonException`, `FormatException` | The JSON payload is invalid, too large or holds a value that cannot be converted. |
| `NotSupportedException` | Oracle: a source column type the provider cannot bind (for example `object`). |

Driver messages (on the `InnerException`) can contain row values, for example SQL Server's
`The duplicate key value is (5)`. Log or show them only under your own data-handling policy.

## Logging

Loki logs through your host's `ILoggerFactory` by default (Serilog, NLog, OpenTelemetry, ...), under categories that
start with `Loki.BulkDataProcessor`:

- **Information:** `Starting bulk copy to {TableName} via {Provider}`, `Completed bulk copy to {TableName} via {Provider}`,
  setting changes (`Setting BatchSize from {OldValue} to {NewValue}`), JSON reader progress, and for Oracle direct path
  `Direct-path load (staging tables only) to {TableName} via {Provider}`.
- **Warning:** a save failed inside your transaction and was left for you to commit or roll back.
- **Error:** `Bulk copy to {TableName} via {Provider} failed with {ExceptionType} (inner {InnerExceptionType})`, and
  rollback or dispose failures (exception types only).
- **Debug:** skipped model properties (names only), Oracle chunk counts.

Loki never logs row values, driver messages or connection strings, and table names are sanitised. To silence Loki, set
`options.UseHostLoggerFactory = false` or add a log filter for the category `Loki.BulkDataProcessor`.

## SQL Server

- Driver: `Microsoft.Data.SqlClient` 6.1.8 (the same major line as EF Core 10). It defaults to **`Encrypt=true`**. For a
  development server with a self-signed certificate add `TrustServerCertificate=true` to the connection string; do not
  turn encryption off.
- Rows are copied with `SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls`: check and foreign key
  constraints are enforced, and an explicit NULL stays NULL instead of taking the column default.
- **INSERT triggers do not run** (`FireTriggers` is not set, as in 8.x). Do not rely on triggers for auditing or derived
  data on tables you load with Loki.
- `SqlBulkCopy` parses and brackets the destination name itself, so `dbo.Posts` and `[dbo].[Posts]` both work. Column
  mappings are case-sensitive.
- A duplicate key fails with SQL Server error 2627 or 2601 in Loki's message; the driver text with the key value is only
  in the `InnerException`.
- An invalid table name is reported as a failed copy (`InvalidOperationException`).

## Oracle

```csharp
services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(Program).Assembly))
        .AddOracle();                                                           // "Oracle": the defaults

IBulkProcessor processor = factory.Create(OracleProvider.Name, oracleConnectionString);
await processor.SaveAsync(posts, "Posts", ct);
```

- Minimum Oracle Database **19c**. Tested on Oracle Database Free **23.26** (`gvenzl/oracle-free:23.26.3-slim-faststart`)
  with `Oracle.ManagedDataAccess.Core` 23.26.400.
- Default write mode `OracleWriteMode.ArrayBind`: a conventional-path `INSERT ... VALUES (:1, :2, ...)` executed once per
  chunk with array binding, inside Loki's or your transaction. Primary, unique, foreign key and check constraints are
  enforced and triggers fire, as on SQL Server with Loki's options.
- **Each chunk is all-or-nothing.** Loki sets a savepoint before every chunk and, if any row of the chunk fails, rolls back
  to it (ODP.NET would otherwise keep the chunk's valid rows). That never ends your transaction. With Loki's own
  transaction the whole save is rolled back; **in your transaction, chunks that succeeded before the failure stay in it**,
  so roll back after a failed save unless you mean to keep them.
- **Memory:** unlike `SqlBulkCopy`, which streams, each chunk is buffered before it is sent. A chunk is at most 20,000 rows,
  and is also cut short at 1,000,000 values (rows x columns) or about 64 MB of string and binary data; the rows are still
  written in the same transaction.
- **Timeouts and cancellation need out-of-band breaks.** ODP.NET delivers a timeout or cancellation to the server as a break.
  When out-of-band breaks are disabled (`DISABLE_OOB=ON` in `sqlnet.ora`, common in containers, including the gvenzl images),
  the server acts on it only when the current call returns: a chunk waiting on another session's lock is not interrupted
  until the lock is released.
- Errors name the Oracle code: `Bulk copy to 'Posts' failed (Oracle error ORA-02291). See InnerException for details.`.
  When a row of a multi-row chunk fails, the code is the row's error (ORA-00001, ORA-02291, ...), not ODP.NET's ORA-24381
  envelope. Some codes add a data-free hint (casing, long strings, timeout, direct path). The `OracleException` is the
  `InnerException`.
- **Invalid table name:** a destination table or schema name that is not a valid identifier throws `ArgumentException`
  (`ParamName` `destinationTableName`) before any SQL runs, where SQL Server reports a failed copy
  (`InvalidOperationException`). An invalid destination **column** is an `InvalidOperationException` starting with the
  column-mapping message on both.

### Options

```csharp
.AddOracle(o =>
{
    o.WriteMode = OracleWriteMode.ArrayBind;                          // default; DirectPath: staging tables only
    o.IdentifierResolution = OracleIdentifierResolution.Unquoted;     // default; CatalogLookup for quoted mixed-case names
    o.BooleanBinding = OracleBooleanBinding.Number;                   // default; Boolean for 23ai BOOLEAN columns
}, name: "Oracle")
```

The options are copied when `AddOracle` runs. Register differently configured providers under different names.

### Identifiers and casing

Destination names can come from untrusted input (a JSON payload), so the Oracle provider never puts a caller's name into
SQL unchecked:

- **`Unquoted` (default).** Each name (`TABLE`, `SCHEMA.TABLE` and every destination column) must be a simple identifier:
  a letter, then letters, digits, `_`, `$` or `#`, at most 128 characters. It is folded to upper case, exactly as Oracle
  folds unquoted names, and then quoted: `Posts` becomes `"POSTS"`, `app.Posts` becomes `"APP"."POSTS"`. So `Posts`,
  `posts` and `POSTS` all mean the table created as `CREATE TABLE Posts`, and reserved words such as `Date` or `Comment`
  work as column names. Anything else is rejected before any SQL runs.
- **`CatalogLookup`.** Looks the table and its columns up in `ALL_TAB_COLUMNS` (exact match first, then a single
  case-insensitive match) and quotes the names the catalogue returns. Use it for quoted mixed-case objects such as
  `"MixedCase"`; wrap a name in double quotes to require an exact match. Your names are only ever sent as bind variables.
  It costs one dictionary query per save (a second, slower one only when the first finds nothing), honours `Timeout`, and
  covers tables and views, not synonyms. A quoted name that contains a dot is not supported.

A name error in `Unquoted` mode adds a hint about casing and `CatalogLookup`.

### Types

| .NET type | Bound as | Notes |
|-----------|----------|-------|
| `string` | `VARCHAR2` | `""` is stored as **NULL** (Oracle semantics). A value must fit a `VARCHAR2(n)` column (ORA-12899 otherwise). |
| `char` | `VARCHAR2` | One character. |
| `bool` | `NUMBER` 1/0 (`Number`, default) or `BOOLEAN` (`Boolean`) | `Number` works with `NUMBER(1)` on 19c+ and, on 23ai, also with a `BOOLEAN` column. `Boolean` binding needs 23ai. Not used by direct path. |
| `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` | `NUMBER` | Widened where needed; `ulong` as decimal. |
| enums | their underlying integer type | |
| `decimal` | `NUMBER` | Full 28-digit precision. |
| `double`, `float` | `BINARY_DOUBLE`, `BINARY_FLOAT` | |
| `DateTime` | `TIMESTAMP` | All 7 fractional digits are kept in `TIMESTAMP(7)`. |
| `DateTimeOffset` | `TIMESTAMP WITH TIME ZONE` | The offset is kept. |
| `TimeSpan` | `INTERVAL DAY TO SECOND` | |
| `DateOnly` | `DATE` (midnight) | Array bind only; direct path rejects it. |
| `TimeOnly` | `INTERVAL DAY TO SECOND` | Array bind only; direct path rejects it. |
| `Guid` | `RAW(16)` | Stored in .NET `Guid.ToByteArray()` order: `00112233-4455-6677-8899-aabbccddeeff` is stored as `33221100554477668899AABBCCDDEEFF`, so other tools reading `RAWTOHEX` see a different value. |
| `byte[]` | `RAW` | Up to the RAW limit; `BLOB` is not supported. |
| `object` and anything else | | `NotSupportedException` before anything is written. |

- **Typed input:** a `DataTable` column without a `DataType` is a string column, and Oracle does not convert `"True"` into
  `NUMBER(1)` (ORA-01722) or a culture-formatted date into `TIMESTAMP`, as `SqlBulkCopy` does. Give `DataColumn`s their
  real type. JSON columns are typed by the payload's `columns` list.
- **Long strings / CLOB:** strings are bound as `VARCHAR2` in the database character set. `CLOB` columns accepted longer
  values on Oracle 23.26 (tested to 100,000 characters with `MAX_STRING_SIZE = STANDARD`); an older server may reject a
  value over the `VARCHAR2` bind limit (4000 bytes, or 32767 with `MAX_STRING_SIZE = EXTENDED`) with ORA-01461.
  `NVARCHAR2` columns work, but characters outside the database character set may be replaced when it is not AL32UTF8.

## Oracle direct path

> **⚠️ Direct path: staging tables only**
>
> `OracleWriteMode.DirectPath` (`OracleBulkCopy`) is opt-in and meant **only for bulk loads into staging tables**. Do not
> use it for tables other applications read or write, for tables whose foreign or unique keys you rely on, or with a
> destination chosen by untrusted input (for example a JSON payload's `tableName`). It skips checks that the default
> array bind performs, and its failures can leave the table needing manual repair.

It is faster on indexed tables (see [Performance](#performance)) because it builds indexes once at the end and skips
foreign key checks. The hazards, all verified on Oracle 23.26:

1. **Other sessions are blocked.** Until the transaction that ran the load ends, other sessions' queries on the table wait
   (library cache lock). Commit right after the save; with Loki's own transaction this lasts only for the save.
2. **Foreign keys are not enforced.** Orphan rows are loaded, and the constraint still reports itself as `VALIDATED`.
3. **Duplicate keys are not detected.** The save succeeds; at commit Oracle marks the primary or unique key index
   **UNUSABLE**. From then on inserts and deletes fail with ORA-01502 and direct-path loads with ORA-26028 (queries and
   updates of non-key columns still work) until you [repair it](#repairing-an-unusable-index). A **DEFERRABLE** primary or
   unique key is worse: the duplicates are kept and the constraint is silently **DISABLED**. Rolling back without reading
   the table leaves the index VALID. Load only rows whose keys you know to be unique.
4. **Never read the loaded table in the same transaction and then roll back.** On 23.26 the next direct-path load into the
   table then returned the rolled-back rows instead of its own. If it happened, empty the table with
   `TRUNCATE TABLE ... DROP ALL STORAGE` before the next load; a plain `TRUNCATE` is not enough.
5. **An index that is already UNUSABLE** (unique or not) makes the load fail with ORA-26028.
6. **Tables with triggers are refused** (ORA-26086).
7. **One load per transaction** (ORA-39822): a second save in the same transaction fails, also into another table and also
   after a failed load. Commit or roll back between loads. The transaction stays usable for other statements.
8. **Synchronous and not cancellable once started.** ODP.NET has no asynchronous direct-path API: the load blocks the calling
   thread and the token is checked only before it starts. `Timeout` is passed to the driver but is **not** an upper bound:
   a load waiting on another session's lock ran until the lock was released and only then failed with ORA-50057.
9. `BatchSize` 0 means 5000 rows per batch. A failed load leaves none of its rows behind.
10. `BooleanBinding` does not apply (the driver converts `bool` itself: 1/0 into `NUMBER(1)`, TRUE/FALSE into `BOOLEAN`).
    `DateOnly` and `TimeOnly` are rejected before the load; use `DateTime` / `TimeSpan`.

**Register it separately** from the default provider and use it only from code that targets staging tables:

```csharp
services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(Program).Assembly))
        .AddOracle()                                                                           // "Oracle": array bind
        .AddOracle(o => o.WriteMode = OracleWriteMode.DirectPath, name: "Oracle.DirectPath");  // staging tables only

// Only in the staging-load code path, with a table name chosen by your code:
IBulkProcessor staging = factory.Create("Oracle.DirectPath", oracleConnectionString);
await staging.SaveAsync(jsonStream, "STG_ORDERS", ct);     // never SaveAsync(jsonStream): the payload would pick the table
```

Each direct-path save logs `Direct-path load (staging tables only) to {TableName} via {Provider}` at Information.

### Repairing an UNUSABLE index

A direct-path load of duplicate keys leaves the key index UNUSABLE after commit (Loki's error then says
`An index of the destination table is UNUSABLE ...` with ORA-01502 or ORA-26028). `ALTER INDEX ... REBUILD` fails with
ORA-01452 while the duplicates exist, and `DELETE` fails with ORA-01502 while the index is UNUSABLE (also with
`SKIP_UNUSABLE_INDEXES`), so repair it in this order. The names below (`T`, `PK_T`, `ID`, `CHILD`, `FK_CHILD_T`) are
placeholders.

Do it in a **maintenance window with every writer of the table stopped**. A `LOCK TABLE` in the repair session does not
protect the whole repair: every DDL statement below commits, which releases the lock.

0. Record what you will need:

   ```sql
   SELECT index_name, status FROM user_indexes WHERE table_name = 'T';
   SELECT DBMS_METADATA.GET_DDL('INDEX', 'PK_T') FROM dual;       -- storage, tablespace, partitioning
   SELECT owner, table_name, constraint_name                      -- foreign keys that reference the key
     FROM all_constraints WHERE constraint_type = 'R' AND r_constraint_name = 'PK_T';
   ```

1. Disable the constraint. If foreign keys reference it this fails with ORA-02297; then use `CASCADE`, which also
   disables those child foreign keys (the list from step 0):

   ```sql
   ALTER TABLE t DISABLE CONSTRAINT pk_t;           -- or: ALTER TABLE t DISABLE CONSTRAINT pk_t CASCADE;
   ```

2. If the index is still there (disabling keeps an UNUSABLE index, and enabling would then fail with ORA-14063), drop it:

   ```sql
   DROP INDEX pk_t;
   ```

3. Delete **only the loaded duplicates**, identified by `ROWID`, never by key alone. Inspect the rows first and check which
   copy is the original: `ROWID` order is not load order in general.

   ```sql
   SELECT ROWID, t.* FROM t WHERE id IN (SELECT id FROM t GROUP BY id HAVING COUNT(*) > 1) ORDER BY id;

   DELETE FROM t WHERE ROWID IN (
     SELECT rid FROM (SELECT ROWID rid, ROW_NUMBER() OVER (PARTITION BY id ORDER BY ROWID) rn FROM t)
     WHERE rn > 1);                                  -- adjust to keep the copies you identified as the originals
   ```

4. Recreate the index with its original definition (the DDL from step 0), then enable the constraint on it:

   ```sql
   CREATE UNIQUE INDEX pk_t ON t (id) /* + the saved storage clauses */;
   ALTER TABLE t ENABLE CONSTRAINT pk_t USING INDEX pk_t;     -- or ENABLE CONSTRAINT pk_t for a default index
   ```

5. Re-enable **and validate** each child foreign key you disabled:

   ```sql
   ALTER TABLE child MODIFY CONSTRAINT fk_child_t ENABLE VALIDATE;
   ```

6. Check the loaded table's own foreign keys too: the load did not check them. Find orphans with an anti-join and fix
   them; `ENABLE VALIDATE` fails with ORA-02298 while orphans exist:

   ```sql
   SELECT t.* FROM t WHERE t.parent_id IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM parent p WHERE p.id = t.parent_id);
   ```

A DEFERRABLE key that the load disabled is repaired the same way (remove the duplicates, then enable it). `TRUNCATE`
also makes an UNUSABLE index VALID again, which is often the simplest fix for a staging table.

## Writing your own provider

The provider interfaces are public, in the `Loki.BulkDataProcessor.Providers` namespace of `LokiBulkDataProcessor.Core`:

- `IBulkCopyProvider`: `Name`, `CreateConnection(connectionString)`, `AdoptExternalTransaction(transaction)` (validate the
  caller's transaction type, throw `ArgumentException` otherwise), `CreateWriter(BulkWriteContext)` and
  `TranslateException(exception, target)`.
- `IBulkWriter`: a single `WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken)`, plus
  `DisposeAsync`. A driver without an async API may complete synchronously, but must check the token before it starts.
- `BulkWriteTarget` (table name as the caller wrote it + `ColumnMap`s), `BulkWriteOptions` (BatchSize, TimeoutSeconds),
  `BulkWriteContext` (open connection, active transaction, options, logger factory) and `BulkCopyErrorMessages` (the shared
  fixed messages and `Sanitize`).
- Register it with a builder extension:

  ```csharp
  public static ILokiBulkDataProcessorBuilder AddMyDb(this ILokiBulkDataProcessorBuilder builder, string name = "MyDb")
  {
      builder.Services.AddSingleton<IBulkCopyProvider>(new MyDbBulkCopyProvider(name));
      return builder;
  }
  ```

Rules every provider follows:

- Stateless and thread-safe: one instance serves every processor.
- Never commit, roll back, dispose or close the connection or transaction it is given; Loki's pipeline owns that.
- `TranslateException` returns a **new** exception whose `InnerException` is the original, with a fixed, data-free message
  (`BulkCopyErrorMessages.BulkCopyFailed(table, "MyDb", code)`). If it drops the original or throws, Loki replaces it with
  `BulkCopyFailed(table)`. Return the exception itself only for your own pre-write validation errors.
- Validate or quote every identifier: a destination table name can come from a JSON payload.

The repository's contract test suite (`LokiBulkDataProcessor.IntegrationTests.Contract`) is the behaviour every built-in
provider passes; run it against your provider the same way the SQL Server and Oracle test projects do.

## Performance

Benchmark: 100,000 records inserted into an empty SQL Server table with 4 columns (1 primary key, 1 foreign key), on a 2.7GHz i7, 16 GB RAM, SSD.

| Method                | Time Taken    | Memory Usage |
|-----------------------|---------------|--------------|
| Loki Bulk Processor   | 2.99 seconds  | 266 MB       |
| Entity Framework Core | 68.74 seconds | 1 GB+        |

Inserted 100,000 records into an empty table with 1 PK and 1 FK column.

> ⚠️ Entity Framework was used with `AddAsync` and `SaveChangesAsync`. Loki used a single bulk insert call.

**Oracle (9.0.0).** 1,000,000 rows of six columns, both databases in local Docker on one 4-vCPU machine (Oracle Free 23.26
in NOARCHIVELOG, SQL Server 2022, each capped at about 2 GB), median of 3 runs:

| Table | Oracle array bind (chunk 5000) | Oracle direct path | SQL Server `SqlBulkCopy` |
|-------|-------------------------------:|-------------------:|-------------------------:|
| No indexes | 2.7 s | 2.1 s | 3.2 s |
| PK + index + FK | 7.9 s | 3.1 s | 8.6 s |

Read these as ratios, not absolute throughput: array bind is about 1.3x slower than direct path on a heap table and about
2.5x slower on an indexed table with a foreign key, and on par with `SqlBulkCopy` (which also enforces the FK). Most of
direct path's lead comes from what it skips (index maintenance per row, the FK check). NOARCHIVELOG favours direct path,
no network latency was measured (array bind makes one round trip per chunk), and chunk sizes above 5000 gained nothing.

## Upgrade guide

| From Version | What Changed                                      | Action Required         |
|--------------|--------------------------------------------------|--------------------------|
| ≤ 6.0.0      | Web API execution failed after the first request | **Upgrade to v7.0.0+** ✅ |
| 6.0.0        | `DataTableMapping` renamed to `DataMapping`      | **Update references** ⚠️ |
| 7.0.1        | Project upgraded to .NET 10                       | **No action required unless you target an earlier runtime** ✅ |
| 8.x          | 9.0.0: see the tables below                       | **Read [8.x to 9.0.0](#8x-to-900)** ⚠️ |

### Upgrading from 8.x in 5 minutes

Most 8.x applications only need to update the package and change the SQL client namespace. This still works unchanged:

```csharp
// 8.x and 9.0
services.AddLokiBulkDataProcessor(connectionString, typeof(Startup).Assembly);

public class ImportService(IBulkProcessor processor)
{
    public Task ImportAsync(IEnumerable<Order> orders) => processor.SaveAsync(orders, "dbo.Orders");
}
```

1. Update `LokiBulkDataProcessor` to 9.0.0.
2. Replace `using System.Data.SqlClient;` with `using Microsoft.Data.SqlClient;` wherever you create connections or
   transactions for Loki, and add `TrustServerCertificate=true` for development servers with self-signed certificates.
3. Replace any use of `IBulkProcessor.ConnectionString` / `WithConnectionString` with the factory (below).
4. In your catch blocks, roll back your own transaction: Loki no longer does it for you.
5. Rebuild: `SaveAsync` gained an optional `CancellationToken`, so mocks and custom `IBulkProcessor` implementations
   need updating.

The 9.0 style, for new code or more than one database:

```csharp
services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(Startup).Assembly))
        .AddSqlServer();

public class ImportService(IBulkProcessorFactory factory)
{
    public Task ImportAsync(IEnumerable<Order> orders, string connectionString, CancellationToken ct) =>
        factory.Create(SqlServerProvider.Name, connectionString).SaveAsync(orders, "dbo.Orders", ct);
}
```

### 8.x to 9.0.0

**Packages, driver and API**

| What changed | Action required |
|--------------|-----------------|
| The driver is now `Microsoft.Data.SqlClient` 6.1.8 (was the deprecated `System.Data.SqlClient`, #14), so EF Core transactions (`GetDbTransaction()`) are accepted. It defaults to `Encrypt=true`. A `System.Data.SqlClient` transaction now throws `ArgumentException`. | Change `using System.Data.SqlClient` to `using Microsoft.Data.SqlClient` wherever you create connections or transactions for Loki. For dev servers with self-signed certificates add `TrustServerCertificate=true`; do not turn encryption off. ⚠️ |
| The mapping API (`ModelMapping<T>`, `DataMapping`, ...), `IBulkProcessor` and `LokiLoggingOptions` moved to the new `LokiBulkDataProcessor.Core` package with **unchanged namespaces**. `LokiBulkDataProcessor` depends on it, and type forwarders let assemblies built against 8.x (for example a separate mappings library) keep binding. | None for source code. Assemblies compiled against 8.x that call `SaveAsync` must be recompiled (see the `CancellationToken` row). ✅ |
| New packages: `LokiBulkDataProcessor.Core` and `LokiBulkDataProcessor.Oracle`. The SQL package depends on Core `[9.0.0, 10.0.0)`. | None; add `LokiBulkDataProcessor.Oracle` to write to Oracle. ✅ |
| `IBulkProcessor.ConnectionString` and `WithConnectionString` are removed. A processor's connection string is fixed when it is created, and there is no public getter for it. | Use `AddLokiBulkDataProcessor(connectionString, ...)` for one database, or inject `IBulkProcessorFactory` and call `Create(SqlServerProvider.Name, connectionString)` per database. ⚠️ |
| Every `SaveAsync` overload takes an optional `CancellationToken` as its last parameter, and `IBulkProcessor` has a new `ProviderName` property. Source-compatible for callers; a binary break. | Recompile. Update custom `IBulkProcessor` implementations and mock setups. ⚠️ |
| `BulkProcessor` is internal (created only by the factory). `ICommandFactory`, the command interfaces, `BulkCopyFromDataReaderCommand`, `IBulkProcessorCommand`, `IAppContext`, `ILokiDbConnection`, `IBulkCopyCommand` and `DbConstants` are removed; `IDataMappingCollection`, `IModelMappingCollection` and `AbstractMappings<T>` are internal. | Depend on `IBulkProcessor` / `IBulkProcessorFactory` only. ⚠️ |
| The empty public class `Loki.BulkDataProcessor.Properties.AssemblyInfo` is removed, and the assembly version is now generated (`9.0.0.0`, was a hard-coded `1.0.0.02`). | Remove any reference to `AssemblyInfo`: `using Loki.BulkDataProcessor.Properties;` no longer compiles. ⚠️ |
| New public API (additive): `IBulkProcessorFactory`; `AddLokiBulkDataProcessor(options => ...)` with `.AddSqlServer()` / `.AddOracle()`; `LokiBulkDataProcessorOptions` (incl. `RequireDataMappingForJson`, `MaxJsonPayloadBytes`); `SaveAsync(IDataReader, destinationTableName, sourceName, ...)`; `SaveAsync(Stream, destinationTableName, ...)`; the provider SPI in `Loki.BulkDataProcessor.Providers`. | None. ✅ |
| `Testcontainers.MsSql` (#17) and `Microsoft.Extensions.DependencyInjection` (now only `.Abstractions`) are no longer transitive dependencies. `Microsoft.Data.SqlClient` brings its own (Azure.Identity, Microsoft.Identity.Client, the SNI runtime). | Reference them directly if your code used them. ✅ |

**Registration and mappings**

| What changed | Action required |
|--------------|-----------------|
| `AddLokiBulkDataProcessor` (any overload) may be called only once; a second call throws `InvalidOperationException`. | Combine providers on one builder: `.AddSqlServer().AddOracle()`. ⚠️ |
| The legacy `AddLokiBulkDataProcessor(connectionString, ...)` throws `ArgumentException` at registration for a blank connection string. | Make sure the connection string is configured at startup. ✅ |
| Mappings are instantiated and validated at registration, not at the first save, and two mappings for the same model type or `SourceTableName` fail at startup with `InvalidOperationException` (8.x silently used the first). | Remove the duplicate; fix mappings that only failed when first used. ⚠️ |
| One `DataMapping` lookup rule (#16): the explicit `sourceName`, else a non-blank `DataTable.TableName`, else the destination table name. A `DataTable` with an empty `TableName` now uses the destination name. | Check `DataMapping`s whose `SourceTableName` equals a destination you write unnamed `DataTable`s to. ⚠️ |
| Models with get-only (for example computed `=>`) or set-only properties no longer throw `NullReferenceException` (#19): those properties are skipped and **not saved**, and a destination column matched only by one is left NULL. Static properties and indexers are never copied. Mapping a get-only property explicitly throws `ArgumentException` before connecting. | Give any property you need persisted a public getter and setter. ⚠️ |

**Behaviour of a save**

| What changed | Action required |
|--------------|-----------------|
| `Transaction`, `BatchSize` and `Timeout` belong to the processor instance, not the app (#11). Each save works from a snapshot taken when it starts. | None, unless you relied on one scope's settings leaking into another. ✅ |
| On failure Loki no longer rolls back **your** transaction (#12). It never commits, rolls back, disposes or closes a transaction you set. | Roll back (or commit other work) yourself in your catch block. ⚠️ |
| Loki's own connection and transaction are disposed on every failure path (#13). | None. ✅ |
| `Transaction = null` clears the transaction (it used to throw). A transaction of another driver throws `ArgumentException` when it is set. | None. ✅ |
| `SaveAsync` argument errors are thrown synchronously, not from the returned `Task`. | If you only `await`, nothing changes; code that inspects the `Task` for argument errors must catch around the call. ✅ |
| Failed copies have a fixed, data-free message: `Bulk copy to '{table}' failed (SQL Server error {Number}). See InnerException for details.` (without the parenthesis when there is no `SqlException`) instead of the driver's message, and the driver exception is the `InnerException` (#15). The column-mapping message is unchanged. | Inspect `InnerException` (for example `SqlException.Number`) instead of parsing `Message`. ⚠️ |
| A connection that cannot be opened (or a transaction that cannot begin) throws `InvalidOperationException` (`Could not open a connection or begin a transaction for provider '...'`) with the driver exception as `InnerException`. | Catch `InvalidOperationException` instead of `SqlException`, and read `InnerException`. ⚠️ |
| A cancelled save throws `OperationCanceledException` with the driver exception as `InnerException`, instead of the driver's exception. | Catch `OperationCanceledException`. ⚠️ |
| `IBulkProcessorFactory.Create` with an unknown provider throws `ArgumentException` without listing the registered names. | Use `factory.ProviderNames` or `HasProvider`. ✅ |
| SQL Server copies still run without `FireTriggers` (unchanged from 8.x, now documented): INSERT triggers do not run. | None; do not rely on triggers for tables loaded with Loki. ✅ |

**JSON**

| What changed | Action required |
|--------------|-----------------|
| Booleans must be JSON `true`/`false` or the strings `"true"`/`"false"` (any case); `1`, `0`, `"yes"` and `""` now throw instead of becoming `false`. | Send real booleans. ⚠️ |
| Dates that are not ISO 8601 are read with the invariant culture (`03/04/2026` is 4 March on every host), not the server's culture. | Send ISO 8601 dates. ⚠️ |
| A value that cannot be converted throws a `FormatException` naming only the column and type, without an `InnerException` (the BCL exception quoted the value). A number that does not fit its type (`1e20` for `int`) throws the same. | Catch `FormatException`; validate payloads before sending them. ✅ |
| The payload is parsed with explicit options (maximum depth 64, no comments or trailing commas), the same as the defaults. New: `SaveAsync(stream, destinationTableName)`, `RequireDataMappingForJson`, `MaxJsonPayloadBytes`. | For payloads you do not fully trust, use `SaveAsync(stream, destinationTableName)` (see [JSON](#json)). ✅ |

**Logging**

| What changed | Action required |
|--------------|-----------------|
| Loki no longer registers `NullLoggerFactory` or console logging for your whole app. It now logs through your host's `ILoggerFactory` by default (start/complete at Information, failures at Error; no row data or connection strings). Loki-private console logging (`LokiLoggingOptions`, `UseConsoleLogging`, the legacy `configureLogging` argument) is obsolete. | To silence it: `options.UseHostLoggerFactory = false`, or a log filter on category `Loki.BulkDataProcessor`. Configure console output in your host's logging. ⚠️ |
| The log category changed from `Loki.BulkDataProcessor.BulkProcessor` to `Loki.BulkDataProcessor.Internal.BulkProcessor`, and the texts are new: `Starting/Completed bulk copy to {TableName} via {Provider}` and `Bulk copy to {TableName} via {Provider} failed with {ExceptionType} (inner {InnerExceptionType})`. The failure entry no longer carries the exception object. | Update log filters, queries and alerts that match the old category or texts. ⚠️ |

## Documentation

- This README is the documentation for 9.x.
- 📝 [Wiki](https://github.com/stephen-rebner/loki-bulk-data-processor/wiki): documentation for 8.x and earlier
- 📺 [YouTube Tutorial – Part 1](https://youtu.be/vci6kQWcmhs) — Introduction & Setup (shows 8.x)
- 📺 [YouTube Tutorial – Part 2](https://youtu.be/2ABIrl_nwMk) — Advanced Usage & Mapping (shows 8.x)
- 🧪 [Example Projects on GitHub](https://github.com/stephen-rebner/loki-bulk-data-processor-examples) (8.x)

## Support

If this library saved you time or made your life easier, consider showing your support:

- ☕ [Buy me a coffee](https://www.paypal.com/paypalme2/StephenRebner?locale.x=en_GB)
- ⭐ Star the [GitHub repo](https://github.com/stephen-rebner/loki-bulk-data-processor) to help others discover it
- 🗣️ Share your experience or contribute feedback via [issues](https://github.com/stephen-rebner/loki-bulk-data-processor/issues)

Every bit helps keep the project going.

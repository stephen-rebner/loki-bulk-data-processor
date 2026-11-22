# Loki Bulk Data Processor samples (9.x)

Runnable examples of the 9.x API described in the [main README](../README.md). They reference the libraries' code (not the
NuGet packages), are part of `LokiBulkDataProcessor.sln`, and CI builds them on every push, so they always compile against the
current API. They are never packed.

| Folder | What it is |
|--------|------------|
| [`Loki.Samples.WebApi`](Loki.Samples.WebApi) | Minimal ASP.NET Core API: per-request engine choice with `IBulkProcessorFactory`, a table allow-list, model / `DataTable` / `IDataReader` / JSON saves with mappings, an EF Core transaction shared with Loki, and an Oracle direct-path staging import. |
| [`Loki.Samples.WebApi.Tests`](Loki.Samples.WebApi.Tests) | Unit tests of the API's own logic (engine choice, allow-list, error mapping). No database. |
| [`Loki.Samples.Benchmark`](Loki.Samples.Benchmark) | Console app: Loki vs EF Core `AddRange` + `SaveChanges` on SQL Server and Oracle. |
| [`docker-compose.yml`](docker-compose.yml) | SQL Server 2022 and Oracle Database Free 23.26 for local runs (the integration tests' digest-pinned images). |
| [`db/oracle/init.sql`](db/oracle/init.sql) | The Oracle schema, run by the Oracle container on its first start. |

You need the .NET 10 SDK and Docker (about 4 GB of memory for both databases; the SQL Server image is `linux/amd64` only, so
Apple silicon needs Rosetta emulation in Docker Desktop).

## 1. Start the databases

```bash
docker compose -f samples/docker-compose.yml up -d --wait    # waits until both are healthy (Oracle: about a minute)
```

- **SQL Server** on `localhost:1433` (`sa`). Its schema comes from the WebApi's EF Core migrations (step 2).
- **Oracle** on `localhost:1521/FREEPDB1`, user `loki_samples`. Its schema (`Customers`, `Orders`, `Products`, the staging table
  `STG_PRODUCTS` and the benchmark's `BenchmarkRows`) is created by `db/oracle/init.sql` on the container's first start.
- The containers have no data volumes: `docker compose -f samples/docker-compose.yml down` deletes every row, and the next `up`
  starts from an empty schema.

> **Local development only.** The default passwords in `docker-compose.yml` (`Loki_Samples_Local_Dev_Only_1`) are public,
> well-known values for these throwaway containers, and the ports are bound to `127.0.0.1`. To use your own, copy
> `samples/.env.example` to `samples/.env` (git-ignored), set the passwords, and set the connection strings to match (below).

## 2. Connection strings

Both samples read `ConnectionStrings:SqlServer` and `ConnectionStrings:Oracle` from configuration. Nothing a caller sends ever
becomes a connection string.

- `appsettings.json` has **no** connection strings or passwords (blank values: an engine without one is "not configured").
- `appsettings.Development.json` has the docker-compose values above, for local development only. `dotnet run` uses the
  Development environment through `Properties/launchSettings.json`. Its SQL Server connection string has
  `TrustServerCertificate=True` for the container's self-signed certificate: that disables certificate validation, so never use
  it anywhere else (see the main README, [SQL Server](../README.md#sql-server)).
- Anything else: **user secrets** (Development) or **environment variables** (any environment), which override the files. Each
  sample has its own user secrets; this is the WebApi's (the benchmark's are in [step 4](#4-run-the-benchmark), with its own
  SQL Server database):

  ```bash
  cd samples/Loki.Samples.WebApi
  dotnet user-secrets set "ConnectionStrings:SqlServer" "Server=localhost,1433;Database=LokiSamples;User Id=sa;Password=<yours>;TrustServerCertificate=True"
  dotnet user-secrets set "ConnectionStrings:Oracle" "User Id=loki_samples;Password=<yours>;Data Source=localhost:1521/FREEPDB1"

  # or, for one shell:
  export ConnectionStrings__SqlServer="..."
  export ConnectionStrings__Oracle="..."
  ```

## 3. Run the WebApi

```bash
cd samples/Loki.Samples.WebApi
dotnet run                       # http://localhost:5080, Development environment
```

In Development, `Samples:ApplyMigrationsOnStartup` is `true`, so the API applies its EF Core migrations to SQL Server at startup
(the setting is ignored outside Development). To apply them yourself instead, with the local `dotnet-ef` tool:

```bash
cd samples
dotnet tool restore
dotnet tool run dotnet-ef database update --project Loki.Samples.WebApi
```

Then send the requests in [`Loki.Samples.WebApi.http`](Loki.Samples.WebApi/Loki.Samples.WebApi.http) (Visual Studio, Rider, or
VS Code with REST Client), or use curl:

```bash
curl -X POST "http://localhost:5080/api/oracle/products/datatable?count=5000"
curl http://localhost:5080/api/oracle/counts
```

### Endpoints

`{engine}` is `sqlserver` or `oracle`; any other value is a 404. Every import runs in Loki's own transaction unless noted.

| Endpoint | Shows |
|----------|-------|
| `GET /api/engines` | The registered providers (`SqlServer`, `Oracle`, `Oracle.DirectPath`) and which engines have a connection string. |
| `GET /api/{engine}/counts` | Row counts of the sample tables (constant SQL). |
| `POST /api/{engine}/customers` | **Models**: a JSON array of `{ "name", "email" }` saved as `IEnumerable<CustomerImportRow>` with a `ModelMapping<T>` (`FullName` → `Name`, ...). |
| `POST /api/{engine}/products/datatable?count=N` | **DataTable**: `TableName = "ProductImport"` selects the `ProductImportMapping` `DataMapping` (`ImportSku` → `Sku`, ...). |
| `POST /api/{engine}/products/reader?count=N` | **IDataReader**: `SaveAsync(reader, "Products", sourceName: "ProductImport")`; the sample disposes its reader, Loki never does. |
| `POST /api/{engine}/json/{table}` | **JSON** (`customers` or `products` only): `SaveAsync(stream, destinationTableName)` with the table chosen by code. The payload's `tableName` is ignored, `RequireDataMappingForJson` limits the columns to the table's `DataMapping`, and bodies over 10 MB get 413 from the endpoint's request size limit (Loki's `MaxJsonPayloadBytes`, the same 10 MB, is a second check whose `InvalidDataException` would be a 400). |
| `POST /api/sqlserver/customers-with-orders` | **Caller-owned transaction** (SQL Server): EF Core inserts a customer and Loki its orders in one `BeginTransactionAsync` transaction, passed to Loki with `GetDbTransaction()`. The sample commits; Loki never commits or rolls it back. `"simulateFailure": true` fails after Loki's save: the sample rolls back and neither the customer nor the orders exist. |
| `POST /api/oracle/products/staging-import?count=N` | **Oracle direct-path staging import**: see below. `includeDuplicate=true` adds a duplicate SKU to show the constraint check in the second step (409). |

Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457) with fixed texts: 400 for invalid input (including
invalid JSON, a value of the wrong type, a payload without a mapped column, or one over Loki's `MaxJsonPayloadBytes` when the
host's request size limit did not stop it first), 404 for an unknown engine or a table that is not
on the allow-list, 409 for a duplicate key, 413 for a body over the limit, 503 for an engine without a provider or connection
string, 500 otherwise. They never contain a connection string, a driver message or row values, and the API logs exception
types only (driver messages can quote row values); `appsettings.json` silences EF Core's `Update` and `Database.Command` log
categories, which would log a failed command's exception.

### The Oracle staging import (direct path)

> **⚠️ Direct path: staging tables only.** `OracleWriteMode.DirectPath` skips checks the default array bind performs, and its
> failures can leave a table needing manual repair. Read [Oracle direct path](../README.md#oracle-direct-path) in the main
> README before you use it.

`OracleStagingImportService` registers direct path as a separate provider (`"Oracle.DirectPath"`) and uses it only for the
constant table `STG_PRODUCTS`, in two steps:

1. **Load:** a direct-path save into `STG_PRODUCTS` in Loki's own transaction, which commits as soon as the load ends (other
   sessions' queries on the table wait only for the load; one load per transaction).
2. **Move:** one transaction of the sample's own, with plain ODP.NET commands and a bind variable for the batch id:
   `INSERT INTO Products ... SELECT ... FROM STG_PRODUCTS WHERE BatchId = :batchId`, then
   `DELETE FROM STG_PRODUCTS WHERE BatchId = :batchId`, then commit. This conventional `INSERT` enforces `Products`' unique and
   other constraints, which the direct-path load does not. If it fails, the batch is rolled back and removed from staging.

`STG_PRODUCTS` is built to stay clear of the direct-path hazards: no primary or unique key and no index (duplicate keys cannot
make an index UNUSABLE), no foreign key, no trigger, and nothing reads it in the load's transaction. A `BatchId` column keeps each
import's rows apart, so one import never moves another's rows, but it does **not** make imports parallel: a direct-path load locks
the table exclusively, so imports run one at a time, and a load also waits for an in-flight move to commit. That wait can neither
be cancelled nor bounded by `Timeout` (README hazard 8). Queries on `STG_PRODUCTS`, including `GET /api/oracle/counts`, wait while
a load runs (hazard 1). The move clears staging with `DELETE` rather than `TRUNCATE`: `DELETE` removes only
its own batch and commits together with the move, while `TRUNCATE` is DDL (it commits on its own and empties every batch).
Because direct-path loads never reuse the space `DELETE` frees, run `TRUNCATE TABLE STG_PRODUCTS DROP ALL STORAGE` from time to
time while no import is running.

## 4. Run the benchmark

```bash
cd samples/Loki.Samples.Benchmark
dotnet run -c Release -- --rows 10000 --engine all --runs 3     # --engine sqlserver | oracle | all
```

For each engine it inserts the same rows into a `BenchmarkRows` table with Loki (one `SaveAsync`, default `BatchSize`; Oracle
uses the default array bind; `Timeout = 0`, because Loki's timeout covers the whole SQL Server copy while EF Core's applies per
batch) and with EF Core 10 (`AddRange` + one `SaveChangesAsync` on a new context, default options), then
prints the median, min and max time, rows per second and allocated memory as a Markdown table. To keep it honest:

- each method runs once untimed first (warm-up: connection pools, JIT, the EF Core model), then `--runs` timed runs;
- the table is emptied (`TRUNCATE`) before every run, and the rows are generated outside the timed section;
- the order of the methods alternates between runs;
- `COUNT(*)` must equal `--rows` after every run, or the benchmark stops with an error;
- both methods run on the same machine against the same database.

On SQL Server the benchmark needs **a database of its own**, `LokiSamplesBenchmark` in `appsettings.Development.json`, which EF
Core's `EnsureCreated` creates on the first run. Do not point it at the WebApi's `LokiSamples` database: `EnsureCreated` does
nothing in a database that already has tables, and the benchmark then stops with an error saying so. On Oracle the table comes
from `db/oracle/init.sql`. Results depend on your machine, Docker's resources and network latency (local containers have almost
none): compare the ratios, not the absolute times.

The benchmark reads `ConnectionStrings:SqlServer` and `ConnectionStrings:Oracle` like the WebApi, but has **its own** user secrets
(a different `UserSecretsId`), so set them in its folder; with `--engine all` an engine without a connection string is skipped:

```bash
cd samples/Loki.Samples.Benchmark
dotnet user-secrets set "ConnectionStrings:SqlServer" "Server=localhost,1433;Database=LokiSamplesBenchmark;User Id=sa;Password=<yours>;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:Oracle" "User Id=loki_samples;Password=<yours>;Data Source=localhost:1521/FREEPDB1"
```

## 5. Stop

```bash
docker compose -f samples/docker-compose.yml down
```

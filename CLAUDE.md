# Loki Bulk Data Processor

A .NET 10 class library, published to NuGet as **`LokiBulkDataProcessor`**, for high-throughput bulk
inserts. Callers hand it `IEnumerable<T>` models, a `DataTable`, an `IDataReader`, or a JSON stream
and it streams the rows into a destination table with the database's native bulk-copy engine.
Today the only released provider is **SQL Server** (`SqlBulkCopy`). **Oracle** (array-bound `INSERT`, with
`OracleBulkCopy` direct path as an opt-in) is being added on `release/9.0`.

This is a **library, not a service**: there is no `Program.cs`, no HTTP surface, no persistence layer
of its own. Every public type is part of a published contract that other people's code compiles
against — treat public API changes as semver decisions.

## 9.0.0 release in progress

A multi-provider 9.0.0 release (Core package, SQL Server provider, Oracle provider) is being built in
nine steps, each a separate PR into the `release/9.0` branch. **Before doing any 9.0.0 work, read
[`docs/plans/9.0.0-progress.md`](docs/plans/9.0.0-progress.md)** (current step status, issue map, approved
decisions) and then the matching section of [`docs/plans/9.0.0-multi-provider.md`](docs/plans/9.0.0-multi-provider.md)
(the spec). The PR that completes a step must also update the progress file. The layout below describes
**`release/9.0` after step 6** (`master` still has the 8.x single-project layout until step 9). The Oracle integration
test project is added in step 7 and Oracle direct path in step 8.

## Repository layout

```
LokiBulkDataProcessor.sln
LokiBulkDataProcessor.Core/                Package LokiBulkDataProcessor.Core (RootNamespace Loki.BulkDataProcessor).
│                                          No database driver. IVT only to UnitTests.
├── IBulkProcessor.cs                      Public: per-instance settings + SaveAsync x6 (models, DataTable, reader,
│                                          reader+sourceName, JSON, JSON+destination), all with an optional CancellationToken
├── IBulkProcessorFactory.cs               Public singleton: Create(providerName, connectionString), HasProvider, ProviderNames
├── Providers/                             Public provider SPI (namespace Loki.BulkDataProcessor.Providers):
│                                          IBulkCopyProvider, IBulkWriter, ColumnMap, BulkWriteTarget, BulkWriteOptions,
│                                          BulkWriteContext, BulkCopyErrorMessages
├── Internal/                              BulkProcessor (internal sealed facade), BulkProcessorFactory,
│                                          BulkProcessorSettings + BulkCallSettings snapshot (#11), LokiLogging(+Selector)
├── Pipeline/                              BulkCopyPipeline (write/commit/rollback/translate), ConnectionScope
│                                          (own vs caller transaction ownership, #12 #13)
├── Sources/BulkSource.cs                  Input shape → IDataReader + ColumnMaps; disposes only readers Loki creates
├── Mappings/                              Public fluent mapping API (ModelMapping<T>, DataMapping, Interfaces/)
│   ├── InternalMapperStorage/             ModelMappings / DataMappings / MappingRegistry (immutable singleton,
│   │                                      duplicate keys fail at registration)
│   ├── Resolution/                        MappingKey (the #16 rule), ColumnMapResolver (mapping → ColumnMaps)
│   └── MappingLogic/                      Validation of mappings (duplicate/empty/null columns)
├── DataReaders/                           JsonDataReader (IDataReader over a JsonDocument) + JsonSchemaValidator
├── DependancyInjection/                   AddLokiBulkDataProcessor(Action<LokiBulkDataProcessorOptions>) →
│                                          ILokiBulkDataProcessorBuilder (sic — folder/namespace misspelled; renaming
│                                          it is a breaking namespace change)
├── Exceptions/, Logging/, Utils/, Constants/DefaultConfigValues.cs
LokiBulkDataProcessor/                     Package LokiBulkDataProcessor = the SQL Server provider (Microsoft.Data.SqlClient)
├── SqlServer/SqlServerProvider.cs         Public: const Name = "SqlServer"
├── SqlServer/Internal/                    SqlServerBulkCopyProvider (IBulkCopyProvider), SqlServerBulkWriter
│                                          (SqlBulkCopy, CheckConstraints|KeepNulls), SqlServerExceptionTranslator.
│                                          The ONLY place SqlClient types are used.
├── DependancyInjection/                   SqlServerBuilderExtensions.AddSqlServer(name), and the legacy
│                                          ServiceCollectionExtension.AddLokiBulkDataProcessor(connectionString, ...)
└── Properties/TypeForwards.cs             TypeForwardedTo Core for the public types that moved in step 3
LokiBulkDataProcessor.Oracle/              Package LokiBulkDataProcessor.Oracle = the Oracle provider (Oracle.ManagedDataAccess.Core)
├── Oracle/                                Public: OracleProvider (const Name = "Oracle"), OracleProviderOptions, OracleWriteMode
│                                          (DirectPath rejected until step 8), OracleIdentifierResolution, OracleBooleanBinding
├── Oracle/Internal/                       OracleBulkCopyProvider, OracleExceptionTranslator (by OracleException.Number),
│   │                                      OracleValidation (marks Loki's own errors so they pass through untranslated), SafeText.
│   │                                      The ONLY place ODP.NET types are used (put `using Oracle.ManagedDataAccess.Client;`
│   │                                      above the file-scoped namespace: `Oracle.` binds to Loki's namespace otherwise)
│   ├── Identifiers/                       OracleIdentifier (validate + quote, CWE-89), Unquoted/CatalogIdentifierResolver,
│   │                                      OracleResolvedTarget
│   ├── Types/OracleTypeMap.cs             .NET type → OracleDbType + value conversion (bool per OracleBooleanBinding)
│   └── Writers/                           ArrayBindOracleWriter (chunks of BatchSize, 0 → 5000), ColumnBuffer,
│                                          OracleInsertSqlBuilder
└── DependancyInjection/                   OracleBuilderExtensions.AddOracle(configure, name)
LokiBulkDataProcessor.UnitTests/           NUnit + Moq + FluentAssertions — no database. TestDoubles/ has
                                           FakeDbConnection/FakeDbTransaction/FakeProvider for pipeline tests; SqlServer/ and
                                           Oracle/ test each provider's internals (all three libraries grant it IVT)
LokiBulkDataProcessor.IntegrationTests.Contract/  Class library (NOT a test project): abstract BulkProcessorContractTests
                                           (partial, one file per topic) + shared models, mappings, ContractRows. Core only;
                                           no driver, no EF. Providers implement its abstract hooks (DB lifecycle, reset, read-back)
LokiBulkDataProcessor.IntegrationTests/    SQL Server: SqlServerContractTests (runs the contract) + SQL-only fixtures (EF Core
                                           transaction, logging, SqlBulkCopy specifics, 8.x legacy registration). NUnit + Testcontainers.MsSql + EF Core
                                           (test schema via EF migrations; SqlServer/SqlServerTestDatabase)
.github/workflows/dotnet.yml               unit + sqlserver-integration jobs on push/PR to master and release/**
.claude/                                   Agents, commands, and standards for Claude Code (see below)
```

## How a call flows

```
IBulkProcessor.SaveAsync(...)              (Internal/BulkProcessor) guard args (ThrowIf*), snapshot settings, log start
  → BulkSource.FromModels/FromDataTable/FromDataReader/FromJson
        mapping = MappingRegistry.Models/Data.GetMappingFor(type | MappingKey.For(sourceName, destination))
        ColumnMapResolver.Resolve(mapping, sourceColumns)    (null mapping → map by name 1:1)
  → BulkCopyPipeline.ExecuteAsync(reader, BulkWriteTarget, BulkCallSettings, ct)
        await using ConnectionScope.OpenAsync   caller's transaction → provider.AdoptExternalTransaction (not owned)
                                                 else provider.CreateConnection + Open + BeginTransaction (owned)
        await using provider.CreateWriter(BulkWriteContext) → writer.WriteAsync(reader, target, ct)
        write failure → provider.TranslateException (new exception, InnerException = original, #15)
        scope.CompleteAsync()                    commits ONLY an owned transaction
      catch → scope.RollbackAfterFailureAsync() (owned only; never throws) → throw;
      dispose → owned transaction + connection only; BulkSource disposes readers Loki created
```

Lifetimes: `IBulkProcessorFactory`, `MappingRegistry`, `LokiLogging` and every `IBulkCopyProvider` are singletons
(immutable, no per-call state). Each `Create` returns a new `IBulkProcessor`; the legacy registration makes
`IBulkProcessor` scoped. Connections, transactions and writers are per call.

**Transaction rule** — if the caller set `IBulkProcessor.Transaction`, Loki never commits, rolls back,
disposes or closes it (or its connection), also when a save fails (#12); the caller owns it. Otherwise
Loki opens, commits (or rolls back) and disposes its own, on every path (#13). This lives once, in
`ConnectionScope`; providers never commit, roll back or dispose the connection or transaction they are given.

**Adding a provider** — implement `IBulkCopyProvider` + `IBulkWriter` with only the public SPI (no IVT from Core),
register it with a builder extension (`AddXxx(this ILokiBulkDataProcessorBuilder, string name = XxxProvider.Name)`),
and keep the driver types in the provider's `Internal/` folder. No `if`/`switch` on provider in Core.

## Public API surface (do not break casually)

`IBulkProcessor`, `IBulkProcessorFactory`, the provider SPI in `Loki.BulkDataProcessor.Providers`, `ModelMapping<T>`,
`DataMapping`, the `Mappings.Interfaces` fluent interfaces, `LokiServiceCollectionExtensions`, `ILokiBulkDataProcessorBuilder`,
`LokiBulkDataProcessorOptions`, `ServiceCollectionExtension.AddLokiBulkDataProcessor`, `SqlServerBuilderExtensions`,
`SqlServerProvider`, `OracleBuilderExtensions`, `OracleProvider`, `OracleProviderOptions`, `OracleWriteMode`,
`OracleIdentifierResolution`, `OracleBooleanBinding`, `LokiLoggingOptions`, and anything else `public` in any library. (`BulkProcessor`, `MappingException`,
`IDataMappingCollection`, `IModelMappingCollection` and `AbstractMappings<T>` are `internal`.) Additive changes are a
minor version; removals/renames/signature changes are a major version and need a README upgrade-guide
row. `[Obsolete]` before removal.

## Commands

```bash
dotnet restore
dotnet build
dotnet test LokiBulkDataProcessor.UnitTests                 # fast, no Docker
dotnet test LokiBulkDataProcessor.IntegrationTests          # needs Docker (Testcontainers SQL Server)
dotnet test LokiBulkDataProcessor.IntegrationTests --filter "FullyQualifiedName~SqlServerContractTests"
dotnet pack LokiBulkDataProcessor -c Release                # GeneratePackageOnBuild is also on
```

Integration tests start **one container per fixture** (`[OneTimeSetUp]`) and reset the data before each test: the SQL
fixtures drop and re-create the database with EF Core migrations from `IntegrationTests/Migrations` (`TestDbContext`).
Provider-neutral behaviour goes in the Contract project's `BulkProcessorContractTests` (ADO.NET base types only, assert only
provider-neutral message parts, no `""` data, never write identity columns or depend on row order); provider-specific behaviour goes in the provider's own test project.

## Conventions

- Namespaces: `Loki.BulkDataProcessor.*` in the library; tests use `LokiBulkDataProcessor.*Tests`.
- Implementation types are `internal` unless they are part of the contract; tests reach them via
  `InternalsVisibleTo`.
- Argument guards use `Utils/Validation/ArgumentValidatorExtensions` (`ThrowIfNullOrEmptyString`,
  `ThrowIfLessThanZero`, `ThrowIfCollectionIsNullOrEmpty`, …) at the public boundary.
- Logging: Loki logs through the host's `ILoggerFactory` by default (`UseHostLoggerFactory = true`), an explicit
  `LoggerFactory` overrides it, and with neither it falls back to `NullLoggerFactory`. Loki never registers an app-wide
  `ILoggerFactory`. Console logging (`LokiLoggingOptions`) is `[Obsolete]`, removed in 10.0. Structured templates only;
  never log connection strings or row data.
- `BulkCallSettings.ConnectionString` is `[DebuggerBrowsable(Never)]` and its `ToString` omits it — keep secrets
  out of debugger views, logs, and exception messages. There is no public connection-string getter.
- Test naming follows the existing style: `Member_Should<Outcome>_When/If<State>`
  (e.g. `Timeout_ShouldThrow_IfValueSetLessThanZero`).
- Full rules: `.claude/standards/dotnet-standards.md`.

## Claude Code tooling (`.claude/`)

| Kind | Name | Purpose |
|------|------|---------|
| Agent | `dotnet-senior` | Implements features/fixes in the library + tests |
| Agent | `dotnet-research` | Read-only planning: affected files, code samples, open questions |
| Agent | `code-review` | Read-only review against `.claude/standards/dotnet-standards.md` |
| Agent | `security-review` | Read-only security review (injection, secrets, transactions, deps) |
| Command | `/plan` | Iterative research rounds → GitHub issue |
| Command | `/research` | One-shot research pass, no code |
| Command | `/build` | Plan → **one approval gate** → implement → PR |
| Command | `/dev` | Straight-through implement → review → PR |
| Command | `/implement` | Implement → mandatory code + security review → summary |
| Command | `/review` | Manual pre-merge checklist |

## CI/CD

`.github/workflows/dotnet.yml` runs on every push and PR to **`master`** (the default branch — not
`main`) and `release/**`, as parallel jobs: `unit` (unit tests + a check that no Testcontainers package
is in the transitive graph of Core, the SQL package or the Oracle package, #17) and `sqlserver-integration` (Testcontainers SQL Server).
Shared build settings (`TargetFramework`, `LangVersion`, `Version`, authors, repo URL) live in
`Directory.Build.props`; shared pack metadata (icon, README, LICENSE, `GeneratePackageOnBuild`) lives in
`build/Package.props`, imported by each library csproj. Bump `<Version>` in `Directory.Build.props`
and `<PackageReleaseNotes>` in the library csproj alongside the README upgrade guide when shipping.

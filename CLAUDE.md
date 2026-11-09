# Loki Bulk Data Processor

A .NET 10 class library, published to NuGet as **`LokiBulkDataProcessor`**, for high-throughput bulk
inserts. Callers hand it `IEnumerable<T>` models, a `DataTable`, an `IDataReader`, or a JSON stream
and it streams the rows into a destination table with the database's native bulk-copy engine.
Today the only provider is **SQL Server** (`SqlBulkCopy`). **Oracle** (`OracleBulkCopy`) is the next
provider being added.

This is a **library, not a service**: there is no `Program.cs`, no HTTP surface, no persistence layer
of its own. Every public type is part of a published contract that other people's code compiles
against — treat public API changes as semver decisions.

## 9.0.0 release in progress

A multi-provider 9.0.0 release (Core package, SQL Server provider, Oracle provider) is being built in
nine steps, each a separate PR into the `release/9.0` branch. **Before doing any 9.0.0 work, read
[`docs/plans/9.0.0-progress.md`](docs/plans/9.0.0-progress.md)** (current step status, issue map, approved
decisions) and then the matching section of [`docs/plans/9.0.0-multi-provider.md`](docs/plans/9.0.0-multi-provider.md)
(the spec). The PR that completes a step must also update the progress file. The layout below describes
**`release/9.0` after step 4** (`master` still has the 8.x single-project layout until step 9). Oracle, the
contract test project and the Oracle test project are added in steps 5-8.

## Repository layout

```
LokiBulkDataProcessor.sln
LokiBulkDataProcessor.Core/                Package LokiBulkDataProcessor.Core (RootNamespace Loki.BulkDataProcessor).
│                                          No database driver. IVT only to UnitTests.
├── IBulkProcessor.cs                      Public: per-instance settings + SaveAsync x5 (models, DataTable, reader,
│                                          reader+sourceName, JSON), all with an optional CancellationToken
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
LokiBulkDataProcessor.UnitTests/           NUnit + Moq + FluentAssertions — no database. TestDoubles/ has
                                           FakeDbConnection/FakeDbTransaction/FakeProvider for pipeline tests
LokiBulkDataProcessor.IntegrationTests/    NUnit + Testcontainers.MsSql + EF Core (test schema via EF migrations)
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
`SqlServerProvider`, `LokiLoggingOptions`, and anything else `public` in either library. (`BulkProcessor`, `MappingException`,
`IDataMappingCollection`, `IModelMappingCollection` and `AbstractMappings<T>` are `internal`.) Additive changes are a
minor version; removals/renames/signature changes are a major version and need a README upgrade-guide
row. `[Obsolete]` before removal.

## Commands

```bash
dotnet restore
dotnet build
dotnet test LokiBulkDataProcessor.UnitTests                 # fast, no Docker
dotnet test LokiBulkDataProcessor.IntegrationTests          # needs Docker (Testcontainers SQL Server)
dotnet test --filter "FullyQualifiedName~BulkProcessorTests"
dotnet pack LokiBulkDataProcessor -c Release                # GeneratePackageOnBuild is also on
```

Integration tests start a fresh container **per test** (`[SetUp]` in `BaseIntegrationTest`) and
build the schema with EF Core migrations from `IntegrationTests/Migrations` against `TestDbContext`.

## Conventions

- Namespaces: `Loki.BulkDataProcessor.*` in the library; tests use `LokiBulkDataProcessor.*Tests`.
- Implementation types are `internal` unless they are part of the contract; tests reach them via
  `InternalsVisibleTo`.
- Argument guards use `Utils/Validation/ArgumentValidatorExtensions` (`ThrowIfNullOrEmptyString`,
  `ThrowIfLessThanZero`, `ThrowIfCollectionIsNullOrEmpty`, …) at the public boundary.
- Logging is optional: every logger parameter defaults to `null` and falls back to
  `NullLogger<T>.Instance`. Structured templates only; never log connection strings or row data.
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
is in the library's transitive graph, #17) and `sqlserver-integration` (Testcontainers SQL Server).
Shared build settings (`TargetFramework`, `LangVersion`, `Version`, authors, repo URL) live in
`Directory.Build.props`; shared pack metadata (icon, README, LICENSE, `GeneratePackageOnBuild`) lives in
`build/Package.props`, imported by each library csproj. Bump `<Version>` in `Directory.Build.props`
and `<PackageReleaseNotes>` in the library csproj alongside the README upgrade guide when shipping.

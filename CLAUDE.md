# Loki Bulk Data Processor

A .NET 10 class library, published to NuGet as **`LokiBulkDataProcessor`**, for high-throughput bulk
inserts. Callers hand it `IEnumerable<T>` models, a `DataTable`, an `IDataReader`, or a JSON stream
and it streams the rows into a destination table with the database's native bulk-copy engine.
Today the only provider is **SQL Server** (`SqlBulkCopy`). **Oracle** (`OracleBulkCopy`) is the next
provider being added.

This is a **library, not a service**: there is no `Program.cs`, no HTTP surface, no persistence layer
of its own. Every public type is part of a published contract that other people's code compiles
against — treat public API changes as semver decisions.

## Repository layout

```
LokiBulkDataProcessor.sln
LokiBulkDataProcessor/                     The NuGet package (RootNamespace: Loki.BulkDataProcessor)
├── BulkProcessor.cs / IBulkProcessor.cs   Public entry point — validates args, logs, delegates to a command
├── Commands/                              One command per input shape (models, DataTable, IDataReader)
│   ├── Factory/                           ICommandFactory / CommandFactory — builds commands per call
│   └── Interfaces/                        IBulkModelsCommand, IBulkDataTableCommand, IBulkCopyFromDataReaderCommand
├── Context/                               IAppContext / AppContext — singleton config (conn string, batch size,
│                                          timeout, external transaction, mapping collections)
├── InternalDbOperations/                  Provider-specific plumbing
│   ├── LokiDbConnection.cs                ILokiDbConnection : IDbConnection — wraps SqlConnection, owns
│   │                                      internal-vs-external transaction semantics
│   ├── BulkCopyCommand.cs                 IBulkCopyCommand — wraps SqlBulkCopy (column mapping, WriteToServerAsync)
│   └── Extensions/TransactionExtensions   Commit/Dispose only when the transaction is internal
├── Mappings/                              Public fluent mapping API
│   ├── ModelMapping<T>, DataMapping        User-subclassed mappings: Map(x => x.Prop).ToDestinationColumn("col")
│   ├── InternalMapperStorage/             ModelMappings / DataMappings — reflection-discovered from mappingAssembly
│   └── MappingLogic/                      Validation of mappings (duplicate/empty/null columns, PK rules)
├── DataReaders/                           JsonDataReader (IDataReader over a JsonDocument) + JsonSchemaValidator
├── DependancyInjection/                   AddLokiBulkDataProcessor(...) IServiceCollection extension (sic — folder
│                                          name is misspelled; renaming it is a breaking namespace change)
├── Exceptions/                            MappingException
├── Logging/                               LokiLoggingOptions (opt-in console logging)
├── Utils/                                 ReflectionUtils, ArgumentValidatorExtensions (ThrowIf* guards)
└── Properties/AssemblyInfo.cs             InternalsVisibleTo the two test projects
LokiBulkDataProcessor.UnitTests/           NUnit + Moq + FluentAssertions — no database
LokiBulkDataProcessor.IntegrationTests/    NUnit + Testcontainers.MsSql + EF Core (test schema via EF migrations)
.github/workflows/dotnet.yml               restore → build → test on push/PR to master
.claude/                                   Agents, commands, and standards for Claude Code (see below)
```

## How a call flows

```
IBulkProcessor.SaveAsync(...)              guard args (ThrowIf*), log start
  → ICommandFactory.NewBulkCopyXxxCommand()
    → IBulkXxxCommand.Execute(...)
        LokiDbConnection.Create(appContext)          opens a new connection, or reuses the external
                                                     transaction's connection
        BeginTransactionIfUsingInternalTransaction() external tx if set, else a new internal one
        mapping = appContext.XxxMappingCollection.GetMappingFor(...)  (null → map by name 1:1)
        dbConnection.CreateNewBulkCopyCommand(tx)    → BulkCopyCommand (SqlBulkCopy, CheckConstraints|KeepNulls)
        bulkCopyCommand.MapColumns(mapping, names)
        await bulkCopyCommand.WriteToServerAsync(...)
        commit / dispose tx + connection ONLY if the transaction is internal
      catch → log, rollback, rethrow
```

**Transaction rule** — if the caller set `IBulkProcessor.Transaction`, Loki never commits, disposes,
or closes it (or its connection); the caller owns it. Otherwise Loki opens, commits and disposes its
own. Any new provider must preserve this exactly.

**Known SQL Server coupling** (relevant to Oracle work): `ILokiDbConnection.CreateNewBulkCopyCommand`
and `CreateCommand` take a `SqlTransaction`; every command casts `(SqlTransaction)transaction`;
`LokiDbConnection` news up `SqlConnection` and casts the external transaction's connection to it;
`BulkCopyCommand` is `SqlBulkCopy`-only; `ThrowInvalidOperationException` pattern-matches a SqlClient
error string; `DbConstants.TempTableName` uses SQL Server `#temp` syntax.

## Public API surface (do not break casually)

`IBulkProcessor` / `BulkProcessor`, `ModelMapping<T>`, `DataMapping`, the `Mappings.Interfaces`
fluent interfaces, `ServiceCollectionExtension.AddLokiBulkDataProcessor`, `LokiLoggingOptions`,
`MappingException`, and anything else `public` in `LokiBulkDataProcessor/`. Additive changes are a
minor version; removals/renames/signature changes are a major version and need a README upgrade-guide
row. `[Obsolete]` before removal (see `WithConnectionString`).

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
- `AppContext.ConnectionString` is `[DebuggerBrowsable(Never)]` — keep secrets out of debugger views,
  logs, and exception messages.
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

`.github/workflows/dotnet.yml` runs `dotnet restore`, `dotnet build --no-restore`,
`dotnet test --no-build` on every push and PR to **`master`** (the default branch — not `main`).
The NuGet package version lives in `LokiBulkDataProcessor.csproj` (`<Version>`, release notes in
`<PackageReleaseNotes>`); bump both alongside the README upgrade guide when shipping.

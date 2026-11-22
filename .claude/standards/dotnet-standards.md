# .NET Coding Standards — Loki Bulk Data Processor

These standards apply to every change in this repository. The libraries are published to NuGet, so
correctness, backward compatibility, and predictable resource handling matter more than anywhere
else.

## Repository layout

```
LokiBulkDataProcessor.Core/                     Package LokiBulkDataProcessor.Core: facade, factory, pipeline, mappings, JSON, SPI. No driver.
LokiBulkDataProcessor/                          Package LokiBulkDataProcessor: the SQL Server provider (Microsoft.Data.SqlClient)
LokiBulkDataProcessor.Oracle/                   Package LokiBulkDataProcessor.Oracle: the Oracle provider (Oracle.ManagedDataAccess.Core)
LokiBulkDataProcessor.UnitTests/                NUnit + Moq + FluentAssertions, no database (IVT from all three libraries)
LokiBulkDataProcessor.IntegrationTests.Contract/  Class library: the provider-neutral contract suite + shared test models/mappings
LokiBulkDataProcessor.IntegrationTests/         SQL Server: runs the contract + SQL-only fixtures (Testcontainers.MsSql, EF Core schema)
LokiBulkDataProcessor.Oracle.IntegrationTests/  Oracle: runs the contract + Oracle-only fixtures (Testcontainers.Oracle, constant DDL)
```

`CLAUDE.md` has the folder-level layout and the call flow; `docs/plans/9.0.0-progress.md` records why things are the way they are.

## Architecture — the layers and where code goes

| Layer | Where | Responsibility |
|-------|-------|----------------|
| Public facade | `Core/IBulkProcessor.cs`, `Core/Internal/BulkProcessor.cs` (internal sealed) | Guard arguments (synchronously), snapshot settings, log start/complete/failure, delegate |
| Factory | `Core/IBulkProcessorFactory.cs`, `Core/Internal/BulkProcessorFactory.cs` | Find a provider by name (case-insensitive), create a new processor per call |
| Settings | `Core/Internal/BulkProcessorSettings.cs` (+ `BulkCallSettings` snapshot) | Per-processor BatchSize/Timeout/Transaction (#11); never app-wide |
| Sources | `Core/Sources/BulkSource.cs` | Input shape → `IDataReader` + `ColumnMap`s; disposes only the readers Loki creates |
| Mappings | `Core/Mappings/` (`InternalMapperStorage/`, `Resolution/`, `MappingLogic/`) | Public fluent API; immutable `MappingRegistry`; the one lookup rule (`MappingKey`, #16); validation |
| Pipeline | `Core/Pipeline/BulkCopyPipeline.cs`, `ConnectionScope.cs` | Open/adopt → write → translate → commit/rollback → dispose. The ONLY place transactions are committed, rolled back or disposed |
| Provider SPI | `Core/Providers/` (public, namespace `Loki.BulkDataProcessor.Providers`) | `IBulkCopyProvider`, `IBulkWriter`, `ColumnMap`, `BulkWriteTarget`, `BulkWriteOptions`, `BulkWriteContext`, `BulkCopyErrorMessages` |
| Providers | `LokiBulkDataProcessor/SqlServer/`, `LokiBulkDataProcessor.Oracle/Oracle/` | Public: `XxxProvider.Name` and options/enums. `Internal/`: the provider, writer(s), exception translator, identifier/type handling |
| DI | `*/DependancyInjection/` (sic) | `AddLokiBulkDataProcessor(options)` → `ILokiBulkDataProcessorBuilder`; `AddSqlServer()`, `AddOracle()`; the 8.x legacy overload |
| Data readers / utils | `Core/DataReaders/`, `Core/Utils/` | `JsonDataReader` + schema validation; guards, reflection, `MessageText` |

Rules:

- **Driver types stay in the provider's `Internal/` folder.** `SqlConnection`, `SqlBulkCopy`, `OracleConnection`,
  `OracleBulkCopy`, `OracleException`, … appear only under `SqlServer/Internal/` or `Oracle/Internal/`. Core references no
  database driver and uses ADO.NET base types (`DbConnection`, `DbTransaction`, `IDataReader`) only.
- **Providers use only Core's public SPI.** Core grants `InternalsVisibleTo` to the unit tests only, never to a provider, so a
  third-party provider can do everything a built-in one does.
- **No `if`/`switch` on provider in Core** (Open/Closed). A new database is a new package with an `IBulkCopyProvider`, an
  `IBulkWriter`, a translator and an `AddXxx` builder extension.
- **No business logic in DI registration.** Builder extensions validate their arguments and register singletons; options are
  validated and copied at registration (`OracleProviderOptions.Validated()`).
- **No shared mutable state.** Constants are `const` or `static readonly`; singletons are immutable after construction.

## Lifetimes

- Singletons, immutable, no per-call state: `IBulkProcessorFactory`, `MappingRegistry`, `LokiLogging`, `JsonPayloadPolicy`
  and every `IBulkCopyProvider` (one instance serves every processor, so providers are stateless and thread-safe).
- A processor is a new instance per `IBulkProcessorFactory.Create`; under the legacy registration `IBulkProcessor` is scoped.
  Processors are not thread-safe.
- Connections, transactions, writers and the readers Loki creates are per call and disposed by Loki.
- Do not change a lifetime without a documented reason (progress file + README if callers can see it).

## Public API and versioning

- Anything `public` in any library is a contract (see the list in `CLAUDE.md`). Prefer `internal` for implementation types;
  tests reach them through `<InternalsVisibleTo Include="..."/>` items in the csproj. `OraclePublicApiTests` pins the Oracle
  package's public surface.
- **Additive only** within a major version: new overloads, new optional parameters at the end of new members, new types. Never
  change an existing public signature, rename a public type/namespace, or change default behaviour silently. (The
  `DependancyInjection` namespace is misspelt; renaming it is a breaking change.)
- Removing or renaming requires `[Obsolete("Use X instead.")]` first, a major version bump, and a row in the README "Upgrade
  guide". Behaviour changes callers can observe (exception types, messages, log categories) also get an upgrade-guide row.
- Every public member has XML docs (`GenerateDocumentationFile` is on; the build must stay at 0 warnings). Document
  exceptions, transaction behaviour and provider differences where they matter.
- The README is the user documentation: update it with the change, and keep every code sample compiling against the
  current API (the factory sample lives verbatim in `UnitTests/Docs/ReadmeFactorySampleTests.cs`).
- Shipping: `<Version>` in `Directory.Build.props` (all three packages share it), `<PackageReleaseNotes>` in each library
  csproj, keep `<PackageTags>` / `<Description>` accurate. Provider packages depend on Core `[Version, next major)`
  (`PinCoreDependencyVersion`).

## Transactions and resource lifetime — non-negotiable

- **Caller transaction (`IBulkProcessor.Transaction` set):** Loki never commits, rolls back, disposes or closes it or its
  connection, on any path, also when the save fails (#12). The caller owns both. `AdoptExternalTransaction` validates the type
  (`ArgumentException` for another driver) and never touches it.
- **Loki's own transaction:** `ConnectionScope` opens the connection, begins the transaction, commits on success, rolls back on
  failure, and disposes both on every path (#13). An open/begin failure disposes the connection and surfaces as
  `InvalidOperationException` (or `OperationCanceledException` after cancellation) with the driver error as InnerException.
- This logic exists **once**, in `ConnectionScope`. Providers and writers never commit, roll back, dispose or close the
  connection or transaction in their `BulkWriteContext`. (A provider may use savepoints inside the transaction, as the Oracle
  array-bind writer does per chunk, because rolling back to a savepoint never ends the transaction.)
- On failure, roll back and **rethrow** (`throw;`, never `throw ex;`). A rollback or dispose failure is logged (exception type
  only) and never masks the original exception.
- Every `IDisposable`/`IAsyncDisposable` Loki creates (connections, commands, parameters, bulk-copy objects, writers, readers it
  creates) is disposed with `using` / `await using` or in a `finally`, also on the exception path.
- Never dispose an `IDataReader` or `Stream` the caller passed in.

## Code design principles

### Method length
Keep methods under ~20 lines. If a method grows beyond that, extract a private method per distinct logical step. The public
entry point reads like a narrative (snapshot settings → build the source → run the pipeline).

### Private methods for related code blocks
Name private methods after what they do, not how (`ResolveTarget`, `BindChunk`, `RollbackAfterFailureAsync`). Keep pure logic
(planning, chunking, identifier resolution) in methods that can be unit-tested without a database, as `DirectPathLoad`,
`CatalogIdentifierResolver.Resolve` and `ArrayBindOracleWriter.CopyInChunksAsync` do.

### Objects own their own state
Settings change only through the processor's guarded properties into its own `BulkProcessorSettings`; each call works from a
`BulkCallSettings` snapshot. Mapping rules (duplicate/empty column checks) live in `Mappings/MappingLogic/`; the lookup rule
lives only in `MappingKey`.

### SOLID reminders
- **Single Responsibility** — the facade validates and delegates; `BulkSource` shapes input; the pipeline owns connections and
  transactions; providers talk to the database.
- **Open/Closed** — add a provider by adding a package, not by adding branches to Core.
- **Liskov** — every provider honours the same transaction, mapping, null-handling and error-translation contract; the shared
  contract suite proves it per provider.
- **Dependency Inversion** — Core depends on `IBulkCopyProvider` / `IBulkWriter` and ADO.NET base types; only a provider's
  `Internal/` code `new`s a driver type.

## Argument validation

- Validate at the public boundary (`Internal/BulkProcessor`, factory, builder extensions) using
  `Utils/Validation/ArgumentValidatorExtensions` (`ThrowIfNullOrEmptyString`, `ThrowIfLessThanZero`, `ThrowIfNull`,
  `ThrowIfCollectionIsNullOrEmpty`, `ThrowIfNullOrHasZeroRows`) or `ArgumentException.ThrowIfNullOrWhiteSpace` /
  `ArgumentNullException.ThrowIfNull`. `SaveAsync` guards throw synchronously.
- Throw `ArgumentException` / `ArgumentNullException` with `paramName` for bad arguments, `InvalidOperationException` for
  runtime/database failures and invalid registrations, `NotSupportedException` for unsupported types. Never throw `Exception`
  or `ApplicationException`.
- A provider's own pre-write validation errors (bad identifiers, unsupported types) are marked so the pipeline passes them
  through untranslated (Oracle: `OracleValidation`).

## Error handling

- `IBulkCopyProvider.TranslateException` returns a **new** exception with a fixed, data-free message built from
  `BulkCopyErrorMessages` (`BulkCopyFailed(table, database, code)`, `ColumnMappingMismatch`) and the original as
  `InnerException` (#15). The pipeline enforces this: a translation that drops the original, or a throwing translator, becomes
  `BulkCopyFailed(table)`; cancellation becomes `OperationCanceledException` without translation.
- Driver messages can quote row values (duplicate keys, conversion errors): never copy them into a Loki message or log entry.
  Echo caller text (table/column names) only through `BulkCopyErrorMessages.Sanitize` / `MessageText.Sanitize`.
- No catch-and-swallow except where the swallowed error is logged and must not mask another (rollback/dispose failures).
- Messages never include the connection string, credentials or row data.

## Logging

- Loki logs through the `ILoggerFactory` chosen at registration (explicit `LoggerFactory` → obsolete console → the host's →
  `NullLoggerFactory`); providers get it from `BulkWriteContext.LoggerFactory`. Loki never registers an app-wide
  `ILoggerFactory`. Classes that log fall back to a no-op logger when none is given.
- Structured templates only:
  ```csharp
  _logger.LogInformation("Starting bulk copy to {TableName} via {Provider}", tableName, provider); // CORRECT
  _logger.LogInformation($"Starting bulk copy to {tableName}");                                     // WRONG
  ```
- Never log connection strings, passwords, wallet paths/secrets, row values, or driver exception objects/messages (log the
  exception **type**). Sanitise logged table names.
- `LogInformation` for lifecycle (start/success), `LogWarning` for expected recoverable issues, `LogError` for failures,
  `LogDebug` for counts and skipped property names. No per-row logging — this is a bulk library.

## Dependency injection

- `AddLokiBulkDataProcessor(Action<LokiBulkDataProcessorOptions>)` is the entry point and may be called once; providers chain
  on its builder (`.AddSqlServer().AddOracle(...)`). The 8.x `AddLokiBulkDataProcessor(connectionString, ...)` keeps working.
- A provider registers itself with `AddXxx(this ILokiBulkDataProcessorBuilder builder, ..., string name = XxxProvider.Name)` as
  a singleton `IBulkCopyProvider`. Named registrations give one engine several configurations.
- No service locator (`IServiceProvider` injected into pipeline or provider classes).

## Dependencies (NuGet)

- No new package without a clear reason. Driver packages are justified; convenience libraries usually are not.
- Test-only packages (Testcontainers, NUnit, Moq, FluentAssertions, Microsoft.NET.Test.Sdk, coverlet, EF Core) belong in test
  projects only — never in a library csproj. CI fails if one reaches a library's dependency graph or a packed nuspec.
- Pin explicit versions. The CI vulnerable-package gate must stay green; a security pin of a transitive package gets a comment
  saying when to drop it.

## C# conventions

- Primary constructors for classes with ≤ 3 dependencies.
- `private readonly` fields for injected dependencies.
- File-scoped namespaces for new files; don't reformat untouched files to convert them. In the Oracle package, put
  `using Oracle.ManagedDataAccess.Client;` above the file-scoped namespace (`Oracle.` binds to Loki's namespace otherwise).
- No `var` for non-obvious types.
- Async methods end in `Async`. No `.Result` / `.Wait()` / `.GetAwaiter().GetResult()`.
- New async APIs take `CancellationToken cancellationToken = default` as the last parameter and flow it to the driver. A
  driver without an async API (Oracle direct path) checks the token before it starts and documents the limitation.
- `ConfigureAwait(false)` on awaits in library code.
- No string concatenation with `+`; use interpolation (or raw string literals) for fixed-shape messages, `StringBuilder` in loops.
- No commented-out code, no `TODO` in production paths.
- Nullable reference types are not enabled project-wide; don't enable them as a drive-by.

## Testing requirements

Every change ships with tests:

1. **Unit tests** (`LokiBulkDataProcessor.UnitTests`) — guards, mapping validation, factory/DI wiring, pipeline ownership rules
   (with `TestDoubles/` fakes and a Moq `IBulkCopyProvider`), error translation, provider internals (identifiers, type map,
   chunking, load plans). No database.
2. **Contract tests** (`LokiBulkDataProcessor.IntegrationTests.Contract`) — **provider-neutral behaviour goes here**, in the
   abstract partial `BulkProcessorContractTests`, so it runs on every provider: every input shape with and without mappings,
   nulls kept, own/caller transactions (#11, #12, #13), the #16 rule, failures, timeouts, cancellation, hostile identifiers,
   secrets. Use ADO.NET base types only, assert only provider-neutral message parts, never use `""` data, never write identity
   columns, never depend on row order. A schema change needs both a new EF migration (SQL Server) and the matching
   `OracleSchema` DDL.
3. **Provider tests** (`LokiBulkDataProcessor.IntegrationTests`, `LokiBulkDataProcessor.Oracle.IntegrationTests`) — behaviour
   only one engine has (EF Core transactions, SqlBulkCopy messages, Oracle types, direct-path hazards).
4. Use FluentAssertions (`.Should()`), NUnit `[Test]` / `[TestCase]`.

Naming: `Member_Should<Outcome>_When/If<State>` (e.g. `Timeout_ShouldThrow_IfValueSetLessThanZero`).

Integration fixtures start **one container per fixture** in `[OneTimeSetUp]` (stopped in `[OneTimeTearDown]`, also after a failed
start), with a random password (`TestPasswords.NewPassword()`) and a tag + digest pinned image, and reset the data before each
test in `[SetUp]`. Use constant SQL with bind variables in fixtures. No `Thread.Sleep`; never hold a database lock without a
bounded release (the Oracle test image sets `DISABLE_OOB=ON`, so a timeout cannot interrupt a wait on a lock).

## Adding a provider — checklist

- [ ] New package `LokiBulkDataProcessor.Xxx` referencing Core (public API only) and the driver; imports `build/Package.props`;
      copies the `PinCoreDependencyVersion` target; grants IVT to the unit tests only; added to `build/pack.sh` and CI.
- [ ] Public: `XxxProvider.Name`, options/enums if needed, `AddXxx(builder, ..., name)`; everything else `internal` under `Internal/`.
- [ ] `AdoptExternalTransaction` rejects another driver's transaction with `ArgumentException` (`ParamName` `transaction`).
- [ ] The writer never commits, rolls back, disposes or closes the context's connection or transaction; it honours BatchSize,
      Timeout and the token, and disposes what it creates.
- [ ] Identifiers are validated or quoted (CWE-89): a destination can come from a JSON payload.
- [ ] `TranslateException` gives fixed, data-free messages with the original as InnerException; own validation errors pass through.
- [ ] A test project that derives from `BulkProcessorContractTests` passes the whole contract unchanged, plus provider-only tests.
- [ ] README section (types, casing, limits, differences from SQL Server) and upgrade-guide rows if anything changes for callers.

## What NOT to do

- No driver types outside a provider's `Internal/` folder, and none in Core.
- No `if`/`switch` on provider in Core.
- No breaking public API changes without `[Obsolete]`, a major version bump and a README upgrade-guide row.
- No committing, rolling back, disposing or closing a caller-supplied transaction or connection — and no transaction ownership
  logic outside `ConnectionScope`.
- No logging or exception messages containing connection strings, credentials, row data or driver messages.
- No test-only packages in a library csproj.
- No string-built SQL from caller input without validating and quoting identifiers; values are always bound.
- No drive-by refactors or reformatting of files the task doesn't touch.

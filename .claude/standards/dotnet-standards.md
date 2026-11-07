# .NET Coding Standards — Loki Bulk Data Processor

These standards apply to every change in this repository. The library is published to NuGet, so
correctness, backward compatibility, and predictable resource handling matter more than anywhere
else.

## Repository layout

```
LokiBulkDataProcessor/                   The package (RootNamespace Loki.BulkDataProcessor)
LokiBulkDataProcessor.UnitTests/         NUnit + Moq + FluentAssertions, no database
LokiBulkDataProcessor.IntegrationTests/  NUnit + Testcontainers + EF Core test schema
```

## Architecture — the layers and where code goes

| Layer | Folder | Responsibility |
|-------|--------|----------------|
| Public facade | `BulkProcessor.cs` / `IBulkProcessor.cs` | Guard arguments, log, pick a command, rethrow |
| Commands | `Commands/` | One class per input shape. Orchestrates connection → transaction → mapping → bulk copy → commit/rollback |
| Command factory | `Commands/Factory/` | Creates a fresh command per call (keeps Web API repeat-execution safe, see v7.0.0) |
| Context | `Context/` | Singleton configuration: connection string, batch size, timeout, external transaction, mapping collections |
| Provider plumbing | `InternalDbOperations/` | The **only** place that may reference a provider's ADO.NET types (`SqlConnection`, `SqlBulkCopy`, …) |
| Mappings | `Mappings/` | Public fluent mapping API + internal storage/validation |
| Data readers | `DataReaders/` | `IDataReader` adapters (JSON) |
| DI | `DependancyInjection/` | `AddLokiBulkDataProcessor(...)` registration |
| Guards / reflection | `Utils/` | Shared helpers |

Rules:

- **Provider types stay in `InternalDbOperations/`.** Commands, the facade, mappings and data readers
  must depend on `IDbConnection` / `IDbTransaction` / `ILokiDbConnection` / `IBulkCopyCommand`, not on
  `SqlTransaction`, `OracleTransaction`, etc. Existing `(SqlTransaction)` casts in commands are known
  coupling to remove as providers are added — do not add new ones.
- **One provider, one implementation set.** A new database = new implementations of the provider
  abstractions (connection, bulk-copy command, error translation), selected at registration time.
  Do not branch on provider with `if`/`switch` inside commands (Open/Closed).
- **No business logic in DI registration.** `ServiceCollectionExtension` only wires types.
- **No new shared static mutable state.** `DbConstants` / `DefaultConfigValues` are legacy
  `public static` fields; new constants are `const` or `static readonly`.

## Public API and versioning

- Anything `public` in `LokiBulkDataProcessor/` is a contract. Prefer `internal` for implementation
  types; tests reach them through `InternalsVisibleTo` in `Properties/AssemblyInfo.cs`.
- **Additive only** within a major version: new overloads, new optional parameters at the end, new
  types. Never change an existing public signature, rename a public type/namespace, or change default
  behaviour silently.
- Removing or renaming requires `[Obsolete("Use X instead.")]` first, a major version bump, and a row
  in the README "Upgrade Guide" table.
- New public members get XML doc comments (`/// <summary>`), matching `ModelMapping<T>.Map`.
- Bump `<Version>` and `<PackageReleaseNotes>` in `LokiBulkDataProcessor.csproj` when a change is
  meant to ship; keep `<PackageTags>` / `<Description>` accurate (e.g. add `oracle` when Oracle ships).

## Transactions and resource lifetime — non-negotiable

- **External transaction (`IBulkProcessor.Transaction` set):** Loki must never commit, roll back on
  success paths, dispose, or close the transaction or its connection. The caller owns both.
- **Internal transaction:** Loki opens the connection, begins the transaction, commits on success,
  rolls back on failure, and disposes both.
- On failure, roll back and **rethrow** (`throw;`, never `throw ex;`) so the original stack trace is
  kept. A rollback failure must not mask the original exception.
- Every `IDisposable` created by Loki (connections, bulk-copy objects, readers it creates) is disposed
  via `using` / `await using` or in a `finally`. No leaked connections on the exception path.
- Do not dispose an `IDataReader` the caller passed in — they own it. Do dispose readers Loki creates
  (`ObjectReader`, `JsonDataReader`).

## Code design principles

### Method length
Keep methods under ~20 lines. If a method grows beyond that, extract a private method per distinct
logical step. The public entry point should read like a narrative (open connection → begin
transaction → resolve mapping → write → complete).

### Private methods for related code blocks
Name private methods after what they do, not how (`ResolveMapping`, `CreateBulkCopy`,
`CompleteTransaction`). The command classes currently repeat the same connection/transaction
sequence three times — when touching them, extract shared steps rather than adding a fourth copy.

### Objects own their own state
Configuration changes go through named methods (`IAppContext.SetBatchSize`, `SetTimeout`,
`SetTransaction`), never via public setters on internal state. Mapping rules (duplicate/empty column
checks, primary key rules) live in `Mappings/MappingLogic/`, not in commands.

### SOLID reminders
- **Single Responsibility** — the facade validates and delegates; commands orchestrate; provider
  classes talk to the database.
- **Open/Closed** — add a provider by adding classes, not by adding `if` branches to existing ones.
- **Liskov** — every provider implementation honours the same transaction, mapping, null-handling
  and error-translation contract; integration tests prove it per provider.
- **Dependency Inversion** — depend on `ILokiDbConnection`, `IBulkCopyCommand`, `IAppContext`,
  `ICommandFactory`; never `new` a provider type outside `InternalDbOperations/`.

## Argument validation

- Validate at the public boundary (`BulkProcessor`) using `Utils/Validation/ArgumentValidatorExtensions`
  (`ThrowIfNullOrEmptyString`, `ThrowIfLessThanZero`, `ThrowIfNull`, `ThrowIfCollectionIsNullOrEmpty`,
  `ThrowIfNullOrHasZeroRows`). Add a new `ThrowIf*` extension rather than inlining checks.
- Throw `ArgumentException` / `ArgumentNullException` with `paramName` for bad arguments,
  `MappingException` for invalid mappings, `InvalidOperationException` for runtime/database failures.
- Never throw `Exception` or `ApplicationException`.

## Error handling

- Translate provider-specific errors into stable, provider-neutral messages where the library already
  does so (see `BulkCopyCommand.ThrowInvalidOperationException`). Preserve the original exception as
  `InnerException` when wrapping — do not throw away the cause.
- No catch-and-swallow (`catch (Exception) { }`). Catching to log + rollback + `throw;` is the pattern.
- Error messages must never include the connection string, credentials, or row data.

## Logging

- Every class that logs takes an optional `ILogger<T> logger = null` and falls back to
  `NullLogger<T>.Instance`. Logging must be zero-cost and zero-config when the caller doesn't opt in.
- Structured templates only:
  ```csharp
  _logger.LogInformation("Starting bulk copy of {Count} rows to {TableName}", count, tableName); // CORRECT
  _logger.LogInformation($"Starting bulk copy of {count} rows to {tableName}");                 // WRONG
  ```
- Never log connection strings, passwords, wallet paths/secrets, or row values.
- `LogInformation` for lifecycle (start/success), `LogWarning` for expected recoverable issues,
  `LogError` (with the exception) for failures. Avoid per-row logging — this is a bulk library.

## Dependency injection

- `AddLokiBulkDataProcessor(...)` remains the single registration entry point. New providers get a
  clearly named, additive entry point or option — existing callers must keep getting SQL Server with
  no code change.
- Lifetimes: `IAppContext` singleton; `ILokiDbConnection` transient; commands, factory and
  `IBulkProcessor` scoped. Do not change an existing lifetime without a documented reason.
- No service locator (`IServiceProvider` injected into commands or provider classes).

## Dependencies (NuGet)

- No new package without a clear reason. Provider packages (e.g. `Oracle.ManagedDataAccess.Core`)
  are justified; convenience libraries usually are not.
- Test-only packages (Testcontainers, EF Core, Moq, FluentAssertions) belong in test projects only —
  never in `LokiBulkDataProcessor.csproj`, because they would flow to every consumer. (The existing
  `Testcontainers.MsSql` reference in the library csproj is a known issue.)
- Pin explicit versions. Check for known CVEs/deprecations (`System.Data.SqlClient` is deprecated in
  favour of `Microsoft.Data.SqlClient` — note it, don't silently swap it, that's a behaviour change).

## C# conventions

- Primary constructors for classes with ≤ 3 dependencies (matches the commands).
- `private readonly` fields for injected dependencies.
- File-scoped namespaces for new files; don't reformat untouched files to convert them.
- No `var` for non-obvious types.
- Async methods end in `Async` (new code — existing `Execute` keeps its name). No `.Result` / `.Wait()`.
- Accept a `CancellationToken cancellationToken = default` as the last parameter on new async APIs and
  flow it into `WriteToServerAsync` / `OpenAsync`. Adding it to existing public signatures is a
  breaking change — add an overload instead.
- `ConfigureAwait(false)` **is** appropriate in this library (it runs in arbitrary host contexts);
  use it on new awaits in library code. Don't mass-edit existing awaits in unrelated code.
- No string concatenation with `+`; use interpolation for fixed-shape messages, `StringBuilder` for loops.
- No commented-out code, no `TODO` in production paths.
- Nullable reference types are not enabled project-wide; don't enable them as a drive-by.

## Testing requirements

Every change ships with tests:

1. **Unit tests** (`LokiBulkDataProcessor.UnitTests`) — guards, mapping validation, factory/DI wiring,
   error translation. Mock `IAppContext`, `ICommandFactory`, commands with Moq. No database.
2. **Integration tests** (`LokiBulkDataProcessor.IntegrationTests`) — real database via Testcontainers.
   For each provider cover: models, `DataTable`, `IDataReader`, JSON stream; with and without
   mappings; nulls preserved; internal transaction commits; external transaction is **not**
   committed by Loki and rolls back when the caller rolls back; failure rolls back and leaves the
   table empty; bad mapping produces the documented error.
3. Use FluentAssertions (`.Should()`), NUnit `[Test]` / `[TestCase]`.

Naming: `Member_Should<Outcome>_When/If<State>` (existing style, e.g.
`Timeout_ShouldThrow_IfValueSetLessThanZero`).

Integration test fixtures follow `Abstract/BaseIntegrationTest`: container per test in `[SetUp]`,
stopped in `[TearDown]`. A new provider gets its own base class (parallel to the SQL Server one)
rather than branching inside the existing base. No `Thread.Sleep`.

## What NOT to do

- No provider-specific types outside `InternalDbOperations/`.
- No breaking public API changes without `[Obsolete]`, a major version bump and a README upgrade note.
- No committing/disposing a caller-supplied transaction or connection.
- No logging or exception messages containing connection strings or data.
- No test-only packages in the library csproj.
- No string-built SQL from caller input without quoting/validating identifiers.
- No drive-by refactors or reformatting of files the task doesn't touch.

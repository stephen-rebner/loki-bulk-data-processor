# /review — Manual Review Checklist

Review the current branch diff against `master`. For each item: ✅ pass, ⚠️ concern (note it), ❌ fail (block merge). Skip sections clearly irrelevant to the diff.

```bash
git diff master...HEAD --stat
git diff master...HEAD
```

Read every changed file in full before marking anything.

---

## 1. Scope & intent

- [ ] The PR does what the description says — no hidden scope.
- [ ] No unrelated changes (reformatting, refactoring, unrelated fixes).
- [ ] Breaking changes called out explicitly.

## 2. Public API & versioning

- [ ] No existing public signature, type, namespace or default behaviour changed (or it's declared breaking with a major bump).
- [ ] Removed/renamed members marked `[Obsolete]` first.
- [ ] New public members have XML docs; implementation types are `internal`.
- [ ] `<Version>`, `<PackageReleaseNotes>`, tags/description and README upgrade guide updated if shipping.
- [ ] Existing SQL Server callers need zero code changes.

## 3. Architecture

- [ ] Provider ADO.NET types only in `InternalDbOperations/`.
- [ ] No new provider casts in commands; no `if`/`switch` on provider in commands or facade.
- [ ] `BulkProcessor` only guards, logs, delegates.
- [ ] Mapping validation stays in `Mappings/MappingLogic/`.
- [ ] `AddLokiBulkDataProcessor` stays backward compatible; lifetimes unchanged.

## 4. Transactions & resources

- [ ] Caller-supplied transaction/connection never committed, disposed or closed by Loki.
- [ ] Internal transaction committed on success, rolled back on failure, disposed on every path.
- [ ] `throw;` used; rollback failure can't mask the original exception.
- [ ] Every Loki-created `IDisposable` disposed, including on exceptions.

## 5. Security

- [ ] No string-built SQL from caller input; identifiers validated/quoted.
- [ ] No connection strings, credentials or row data in logs or exception messages.
- [ ] No TLS weakening in library code.
- [ ] New packages official, pinned, CVE-free; no test-only packages in the library csproj.

## 6. Logging

- [ ] Optional `ILogger<T>` with `NullLogger<T>` fallback.
- [ ] Structured templates only; no interpolation/concatenation.
- [ ] No per-row logging.

## 7. Tests

- [ ] Unit tests for guards, mapping rules, DI/factory wiring, error translation.
- [ ] Integration tests per provider: each input shape, mappings, nulls, internal commit, external tx not committed, failure leaves table empty.
- [ ] Naming `Member_Should<Outcome>_When/If<State>`; FluentAssertions; no `Thread.Sleep`.

## 8. C# code quality

- [ ] New async APIs: `Async` suffix, `CancellationToken cancellationToken = default`, `ConfigureAwait(false)`.
- [ ] No `.Result` / `.Wait()`.
- [ ] Methods ≤ ~20 lines with named private steps.
- [ ] No commented-out code, no `TODO`, no `+` string concatenation.

## 9. Pre-merge checklist

```
[ ] dotnet build — zero errors
[ ] dotnet test LokiBulkDataProcessor.UnitTests — all pass
[ ] dotnet test LokiBulkDataProcessor.IntegrationTests — all pass (Docker)
[ ] No secrets in diff
[ ] README / CLAUDE.md updated if behaviour or structure changed
[ ] PR description accurately reflects the changes
```

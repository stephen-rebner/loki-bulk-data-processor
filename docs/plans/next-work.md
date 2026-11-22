# Next work after 9.0.0

Work agreed with the maintainer after the 9.0.0 release was completed on `release/9.0` (steps 1-9, see
[`9.0.0-progress.md`](9.0.0-progress.md)). **Read this file first in a new session that picks up any of it.**

Status of the release itself: `release/9.0` is complete and green on CI. The maintainer is reviewing it and has **not** approved
the merge to `master` yet. Do not merge `release/9.0` into `master`, tag, or publish anything unless the maintainer asks in that
session. The owner actions (reserve the NuGet IDs, merge, tag, publish) are listed in `9.0.0-progress.md` under
"Before publishing (owner actions)".

| # | Item | Status |
|---|------|--------|
| 1 | Samples project (`samples/`) | **Implemented** on a branch from `release/9.0` (see "Implementation record" below); `code-review` Warnings and most Suggestions fixed; PR open into `release/9.0` (not merged) |
| 2 | NuGet publish workflow | After samples (notes below) |
| 3 | `[Obsolete]` on the unused `IAsPrimaryKey` (remove in 10.0) | 9.x follow-up |
| 4 | Optional allow-list option for Oracle direct path (`OracleProviderOptions`, additive) | 9.x follow-up, deferred in step 8 |

## 1. Samples project

**Goal:** a runnable example that shows off 9.0 the way the README describes it, and that cannot silently rot because CI builds
it. It replaces the 8.x [`loki-bulk-data-processor-examples`](https://github.com/stephen-rebner/loki-bulk-data-processor-examples)
repository as the reference example (the README "Documentation" section links that repo as 8.x).

**Where and how**
- A `samples/` folder in this repository, on a branch cut from `release/9.0` (PR target per the decisions below). Implement with `dotnet-senior`, then review with `code-review`.
- Reference the libraries with **`ProjectReference`** (not the NuGet packages), so the samples always build against the current
  code. Mark every sample project `IsPackable=false`, and make sure `build/pack.sh` still finds exactly three packages.
- Add the sample projects to `LokiBulkDataProcessor.sln` (Any CPU only, like the others), or to a separate `samples/Samples.sln`
  that CI builds; decide and record why.
- CI: build the samples in the `unit` job (or a small `samples` job). Do not run them against databases in CI unless it is cheap.
- No secrets in the repo: connection strings come from configuration / user secrets / environment variables, with only
  local-development values for the docker-compose databases (documented as such).
- Keep the CLAUDE.md conventions (internal by default, short methods, `Member_Should..._When...` test names if samples get tests),
  and keep every sample correct against the README. If a sample duplicates a README snippet, consider pointing the README at it.

**Content** (agreed; the implementing session may refine details and records what it changed)
- `samples/Loki.Samples.WebApi`: a minimal ASP.NET Core API.
  - Registration: `AddLokiBulkDataProcessor(o => o.AddMappingsFrom(...)).AddSqlServer().AddOracle()`, plus a separately named
    direct-path registration (`AddOracle(o => o.WriteMode = OracleWriteMode.DirectPath, name: "Oracle.DirectPath")`).
  - Per-request engine/connection choice through `IBulkProcessorFactory` (the README's Seedr-style pattern), with a table allow-list.
  - A JSON upload endpoint using `SaveAsync(stream, destinationTableName)` with `MaxJsonPayloadBytes` and
    `RequireDataMappingForJson` (never let the payload choose the table).
  - A caller-owned transaction combined with EF Core (`GetDbTransaction()`), showing that Loki never commits or rolls back it.
  - Model, `DataTable` and `IDataReader` saves with `ModelMapping<T>` / `DataMapping`.
  - Oracle staging import: direct-path load into a staging table (with the staging-only warning in comments), commit, then a
    set-based `INSERT ... SELECT` into the real table.
- `samples/Loki.Samples.Benchmark` (console): Loki vs EF Core `AddRange`/`SaveChanges` for N rows on SQL Server (and Oracle array
  bind), printing times. Keep it honest: same machine, warm-up run, row counts checked.
- `samples/docker-compose.yml`: SQL Server and Oracle Free for local runs. Reuse the digest-pinned images from
  `SqlServerTestDatabase.Image` and `OracleTestDatabase.Image`, and give the schema setup (EF migrations for SQL Server, a constant
  DDL script for Oracle).
- `samples/README.md`: how to start the databases and run each sample; link it from the main README's "Documentation" section,
  and keep the 8.x examples-repo link labelled as 8.x (decision 4).

**Decisions (made by the maintainer; do not ask, just build)**
1. Add the sample projects to the main `LokiBulkDataProcessor.sln`, so `dotnet build` and CI build them with everything else.
2. Work on a new branch cut from `release/9.0` and open the PR into `release/9.0` (or into `master` if `release/9.0` has been
   merged by then: check `git log origin/master`).
3. Include the benchmark console app now.
4. Keep the old 8.x examples repository as it is (outside this repo); the README labels it as 8.x and links the new `samples/`.
5. Review: `code-review` only (no security review for the samples), then fix its Critical findings and Warnings before the PR.

**Implementation record** (what the implementing session chose or changed; see `samples/README.md` for usage)
- Projects: `Loki.Samples.WebApi`, `Loki.Samples.WebApi.Tests` (NUnit, no database: engine choice, allow-list, error mapping; kept
  out of `LokiBulkDataProcessor.UnitTests` so the library tests never reference ASP.NET Core) and `Loki.Samples.Benchmark`, in a
  `samples` solution folder of `LokiBulkDataProcessor.sln` (Any CPU only). Nullable and implicit usings are on in the samples
  only (new code; the libraries are unchanged).
- Packages: EF Core 10.0.15 (latest 10.x patch, newer than the integration tests' 10.0.0; no advisories, so no transitive pin is
  needed), `Microsoft.Extensions.Hosting` 10.0.15, `Oracle.EntityFrameworkCore` 10.23.26400 (EF Core 10, same ODP.NET as the
  provider), `dotnet-ef` 10.0.15 as a local tool (`samples/dotnet-tools.json`). No OpenAPI package: a `.http` file instead.
- Connection strings: blank in `appsettings.json`; the docker-compose values (documented local-development-only passwords) in
  `appsettings.Development.json`; user secrets or `ConnectionStrings__*` environment variables override them.
- Schema: SQL Server by the WebApi's EF Core migration (`Samples:ApplyMigrationsOnStartup`, Development only) and, for the
  benchmark, `EnsureCreated` on its own database `LokiSamplesBenchmark`; Oracle by `samples/db/oracle/init.sql`, mounted into the
  gvenzl init directory (it runs as SYSDBA in the CDB root, so it switches to FREEPDB1 and the LOKI_SAMPLES schema).
- Staging import: the direct-path load goes into `STG_PRODUCTS` (no keys, indexes, foreign keys or triggers; a `BatchId` per
  import), the move is `INSERT ... SELECT` + `DELETE ... WHERE BatchId = :batchId` in one transaction (DELETE, not TRUNCATE: it is
  per batch and transactional; periodic `TRUNCATE ... DROP ALL STORAGE` while idle).
- Benchmark on Oracle uses EF Core's Oracle provider (supported on EF Core 10), so no ADO.NET row-by-row baseline was needed.
- CI (`unit` job): builds the solution in Release, runs the samples' tests, validates `samples/docker-compose.yml` and checks that it
  uses the same images as `SqlServerTestDatabase.Image` and `OracleTestDatabase.Image`. After review the three sample projects
  were added to the vulnerable-package gate (no advisories today).
- Code review (no Critical, 6 Warnings) fixed: the benchmark checks that `dbo.BenchmarkRows` exists after `EnsureCreated` (a no-op
  in a database that already has tables) and needs its own database; Loki's `Timeout = 0` in the benchmark (its timeout covers
  the whole SqlBulkCopy, EF Core's is per batch); the benchmark's own user secrets are documented; the exception handler writes
  the problem itself when no `IProblemDetailsWriter` accepts it, and `Microsoft.EntityFrameworkCore.Update` /
  `.Database.Command` are silenced (they log the exception with its driver message). Also: migrations at startup only warn on
  failure (Oracle-only runs work), commits use `CancellationToken.None`, the staging docs say that a direct-path load locks
  `STG_PRODUCTS` exclusively (BatchId separates data, not concurrency; a load waiting on a move cannot be cancelled or bounded),
  and Loki's `MaxJsonPayloadBytes` overflow is documented as 400 (413 comes from the endpoint's request size limit).
- **Verified end to end** (Docker, both engines): `docker compose up -d --wait` is healthy in about 17 s; gvenzl runs `init.sql`
  as SYSDBA in the CDB root (hence `ALTER SESSION SET CONTAINER = FREEPDB1` and `CURRENT_SCHEMA`) and its `healthcheck.sh` stays
  unhealthy until the init scripts are done, so `--wait` covers the schema. 44/44 endpoint checks passed on SQL Server and Oracle:
  every input shape; JSON allow-list (404), malformed/invalid JSON (400), a value of the wrong type (400: Loki surfaces it as
  `InvalidOperationException` with an inner `FormatException`, not a bare `FormatException` as the README's JSON section says), a
  payload missing a mapped column (400: SQL Server reports `ColumnMappingMismatch`, Oracle `SourceColumnNotFound`), 13 MB body
  (413), duplicate keys (409 via SQL error 2627 and ORA-00001 inside ORA-24381); the EF Core transaction (commit, and a simulated
  failure that leaves no customer or orders); the staging import (20,000 rows moved, a duplicate SKU gives 409 and the batch
  is removed from staging, unique index still VALID). Logs: no passwords, no row values. Benchmark `--engine all` passed its
  `COUNT(*)` checks (10,000 rows, median: SQL Server Loki 74 ms vs EF Core 947 ms, Oracle 80 ms vs 369 ms; local Docker).
- Open (library, not samples): the README's JSON section claims a bare `FormatException`, and the two providers word a missing
  mapped source column differently; worth a README fix and a contract test.
- Before publishing: the main README links `tree/master/samples`; those links 404 until the samples reach `master`.

## 2. NuGet publish workflow (after samples)

Advice already given to the maintainer; not started.
- Trigger on a **`v*` tag** (or a published GitHub release), not on every merge to `master` (a re-run would try to push the same
  version, and any merge would ship a release). Optionally fail if the tag does not equal `<Version>` in `Directory.Build.props`.
- **NuGet trusted publishing** (GitHub OIDC, `NuGet/login` action) instead of a long-lived API key; job behind a protected
  `nuget` environment that needs the maintainer's approval; `permissions: id-token: write` only on that job.
- Reuse `build/pack.sh`, add `actions/attest-build-provenance`, then push **Core first**, then the SQL and Oracle packages, with
  `--skip-duplicate`. Pin actions by SHA like the rest of the workflow.
- Prerequisite (owner action): the `LokiBulkDataProcessor.Core` and `.Oracle` IDs must be claimed on nuget.org first (a pushed,
  optionally unlisted `9.0.0-preview.1`, and/or a prefix reservation via account@nuget.org).

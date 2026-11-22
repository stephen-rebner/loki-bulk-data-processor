# Next work after 9.0.0

Work agreed with the maintainer after the 9.0.0 release was completed on `release/9.0` (steps 1-9, see
[`9.0.0-progress.md`](9.0.0-progress.md)). **Read this file first in a new session that picks up any of it.**

Status of the release itself: `release/9.0` is complete and green on CI. The maintainer is reviewing it and has **not** approved
the merge to `master` yet. Do not merge `release/9.0` into `master`, tag, or publish anything unless the maintainer asks in that
session. The owner actions (reserve the NuGet IDs, merge, tag, publish) are listed in `9.0.0-progress.md` under
"Before publishing (owner actions)".

| # | Item | Status |
|---|------|--------|
| 1 | Samples project (`samples/`) | **Next** (planned below, decisions made; to be done in a fresh session without further input) |
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

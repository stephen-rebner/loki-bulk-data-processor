# Spike findings: OracleBulkCopy semantics

Recorded from a throwaway spike (branch `spike/oracle-bulkcopy-semantics`, never merged). These findings drove the 9.0.0 Oracle design.

Run against `gvenzl/oracle-free:23-slim-faststart` with `Oracle.ManagedDataAccess.Core` 23.26.400,
.NET 10, via Testcontainers.Oracle 4.16.0. 

## Transactions
| Scenario | Result |
|---|---|
| Copy on a connection with an open caller `OracleTransaction`, caller rolls back | Copy succeeds; **rows rolled back** (0 left). The copy joins the connection's transaction implicitly — there is no transaction ctor parameter |
| Caller DML first, then copy, same tx | Works (no ORA-26085); rollback undoes both |
| Copy, then caller DML, then commit | Works; both committed |
| Copy fails (CHECK violation) inside caller tx; caller then commits earlier DML | Caller's tx still usable; earlier DML committed; 0 copied rows |
| **Second** copy in the same transaction | **ORA-39822: A new direct path operation is not allowed in the current transaction** |
| `UseInternalTransaction` with an open caller tx | ORA-50006 (InvalidOperationException) |
| No transaction | Auto-committed when `WriteToServer` returns |
| Copy fails mid-load (batchSize 0 or 2) | 0 rows left — no partial batches committed with `Default` options |

## Integrity (direct path load)
| Scenario | Result |
|---|---|
| CHECK constraint violated | Enforced (ORA-02290) |
| **Foreign key violated** | **NOT enforced** — orphan row inserted, FK still reports `ENABLED/VALIDATED` (silent corruption) |
| **Duplicate primary key** | **Load succeeds**, duplicates inserted, PK index becomes **UNUSABLE**; every later plain `INSERT` on the table fails with ORA-01502 until the index is rebuilt |
| Duplicate PK with `EnforceIndexChecks` | **Hung** (session waiting on `enq: TX - contention`); `BulkCopyTimeout = 15` was **not** honoured; killed after ~150s |
| Table has a trigger | Load refused: ORA-26086 "direct path does not support triggers" |

## Identifier casing
| Table / columns given to OracleBulkCopy | Result |
|---|---|
| Table created unquoted (`SPIKE_ROWS`): names `SPIKE_ROWS`, `spike_rows`, `Id`, `id`, `ID`, `"ID"` | **All work** — unquoted names are folded to UPPER, so existing PascalCase mappings work against conventional schemas |
| No mappings, source `Id/Name/Amount` vs columns `ID/NAME/AMOUNT` | Works |
| Quoted mixed-case table `"MixedCase"` with unquoted names | ORA-39826 (resolves to `MIXEDCASE`) |
| `"MixedCase"` table + unquoted columns | ORA-02373 |
| `"MixedCase"` table + `"Id"`, `"Name"` | Works — case-sensitive objects need quoting on table **and** columns |

## Types (23ai)
- `bool` → `BOOLEAN`: works (BOOLEAN column type only exists from 23ai).
- `Guid` → `RAW(16)`: works, stored in .NET `Guid.ToByteArray()` order (first three groups byte-swapped vs the textual GUID) — other tools reading `RAWTOHEX` see a different value.
- `""` → stored as **NULL** (Oracle semantics).
- `DateTime` → `TIMESTAMP`: works. `DBNull` → NULL: works.

## API surface
- `WriteToServer(DataRow[] | DataTable | IDataReader | (DataTable, DataRowState) | OracleRefCursor)` — **all synchronous; no `WriteToServerAsync`, no CancellationToken.**
- Ctors: `(OracleConnection)`, `(string)`, `(OracleConnection, OracleBulkCopyOptions)`, `(string, OracleBulkCopyOptions)` — no transaction parameter.
- Properties: `BatchSize, BulkCopyOptions, BulkCopyTimeout, ColumnMappings, Connection, DestinationTableName, DestinationPartitionName, DestinationSchemaName, NotifyAfter`.
- Options: `Default, UseInternalTransaction, NotifyAllRowsProcessed, EnforceIndexChecks`.

## Throughput benchmark (array-bind vs direct path vs SqlBulkCopy)

Run on 2026-11-15 to decide the default Oracle write mode. The benchmark is the throwaway console app in
`spikes/OracleBulkBenchmark/`. It is not in the solution and is never packed.

**Setup.** One 4-vCPU / 15 GB cloud container, with both databases in local Docker, so there is no network latency.
- Oracle: `gvenzl/oracle-free:23-slim-faststart` (23.26.3, Free edition, so the SGA is capped at 2 GB), **NOARCHIVELOG**,
  ODP.NET 23.26.400.
- SQL Server: 2022-latest with `MSSQL_MEMORY_LIMIT_MB=2048`, SIMPLE recovery, Microsoft.Data.SqlClient 6.1.8.
- Every write runs like Loki's pipeline: open, begin transaction, write from a `DataTable.CreateDataReader()`, commit.
- Six columns (int, int, ~28-character string, decimal, timestamp, small int). Each case is one warm-up run plus 3 timed
  runs; the table shows the median. The table is truncated before every run, and the row count is checked after.
- Tables:
  - `BENCH_HEAP` has no constraints and no indexes.
  - `BENCH_IDX` has a PK, a secondary index on `NAME` and an FK to a 1,000-row parent table.
- SQL Server uses Loki's options, `CheckConstraints | KeepNulls`, so its FK is enforced.

**Median milliseconds** (lower is better; rows/s in brackets):

| Rows | Table | Oracle array-bind, chunk 5000 | Oracle `OracleBulkCopy` | SQL Server `SqlBulkCopy` |
|---:|---|---:|---:|---:|
| 10k | heap | 47 (213k/s) | 62 | 73 |
| 10k | PK + index + FK | 73 (137k/s) | 52 | 97 |
| 100k | heap | 300 (334k/s) | 239 | 326 |
| 100k | PK + index + FK | 793 (126k/s) | 341 | 705 |
| 1M | heap | 2,732 (366k/s) | 2,073 | 3,154 |
| 1M | PK + index + FK | 7,856 (127k/s) | 3,084 | 8,579 |

The `OracleBulkCopy` and `SqlBulkCopy` columns use `BatchSize 0`. BatchSize 5000 was within about 10% for Oracle direct path and
was 5-30% slower for SQL Server. Raw results, with every variant: [`benchmarks/oracle-bulk-2026-11-15.tsv`](benchmarks/oracle-bulk-2026-11-15.tsv).

**Array-bind variants (1M rows):**
- Heap: chunk 1000 = 3,221 ms, 5000 = 2,732 ms, 20000 = 2,762 ms.
- PK + index + FK: chunk 1000 = 8,176 ms, 5000 = 7,856 ms, 20000 = 8,602 ms.
- Typed arrays (`int[]`, `decimal[]` and so on) instead of `object[]`: no consistent gain (2,924 / 8,220 ms). The boxing in
  `object[]` buffers isn't the bottleneck.

**`INSERT /*+ APPEND_VALUES */` with array binding** (direct-path INSERT) **does not work** from ODP.NET: on the heap table
it fails with ORA-38910 "BATCH ERROR mode is not supported for this operation". On the FK table Oracle quietly falls back to a
conventional insert (same speed as plain array-bind), because direct-path INSERT doesn't support enabled FKs. Not an option.

**What it shows**
- On a table with **no indexes**, array-bind is about **1.3× slower** than `OracleBulkCopy` (2.7 s vs 2.1 s for 1M rows), and it
  is as fast as `SqlBulkCopy`, or faster.
- On a table with **PK + index + FK**, array-bind is about **2.5× slower** than `OracleBulkCopy` (7.9 s vs 3.1 s for 1M rows). It
  is still **on par with, or slightly faster than, Loki's `SqlBulkCopy`** on the equivalent SQL Server table (8.6 s), which also
  enforces the FK.
- Most of direct path's lead on the indexed table comes from what it skips. It builds the indexes once at the end instead of row
  by row, and it doesn't check the FK at all (see Integrity above). That is the same shortcut that leaves the PK index UNUSABLE
  on duplicate keys.
- Chunk size 5000 is the sweet spot. 1000 is measurably slower and 20000 gains nothing, so the planned `BatchSize 0` → 5000 is right.

**Caveats**
- NOARCHIVELOG favours direct path, because a direct-path load then skips most redo. Most production databases run ARCHIVELOG
  (often with FORCE LOGGING), where that advantage shrinks.
- No network latency was measured. Array-bind makes one round trip per chunk (200 for 1M rows at 5000), so at 1 ms RTT that
  adds about 0.2 s to an 8 s load.
- These are single-machine numbers, both databases capped at about 2 GB. Read them as ratios, not absolute throughput.

**Conclusion for the 9.0.0 design:** array-bind already gives the Oracle provider SQL Server-class bulk throughput with the same
integrity guarantees. That supports keeping ArrayBind as the default and `OracleWriteMode.DirectPath` as the documented opt-in,
for about 1.3-2.5× more speed where FKs, triggers and duplicate keys are known not to matter (staging tables). Any decision to
change this is recorded in `9.0.0-progress.md`.

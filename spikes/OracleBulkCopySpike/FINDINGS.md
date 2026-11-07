# Spike: OracleBulkCopy semantics (throwaway — never merge)

Run against `gvenzl/oracle-free:23-slim-faststart` with `Oracle.ManagedDataAccess.Core` 23.26.400,
.NET 10, via Testcontainers.Oracle 4.16.0. Reproduce:

```bash
cd spikes/OracleBulkCopySpike
TESTCONTAINERS_RYUK_DISABLED=true dotnet test --logger "console;verbosity=detailed" | grep FINDING
```

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

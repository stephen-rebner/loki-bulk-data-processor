# Loki Bulk Data Processor 9.x: documentation has moved to the README

**From 9.0.0, the documentation lives in the
[README](https://github.com/stephen-rebner/loki-bulk-data-processor#readme)** (also shown on each package's nuget.org
page). The pages in this wiki describe **8.x and earlier** and are kept for users of those versions; they are not updated
for 9.x.

**What changed in 9.0.0.** Loki is now multi-provider. It ships as three packages with the same version:
`LokiBulkDataProcessor` (the SQL Server provider, now on `Microsoft.Data.SqlClient`), the new `LokiBulkDataProcessor.Oracle`
(array-bound inserts by default, with an opt-in direct path for staging tables only) and the new
`LokiBulkDataProcessor.Core` they share. You register providers with
`AddLokiBulkDataProcessor(options => ...).AddSqlServer().AddOracle()` and create processors per database with the
injectable `IBulkProcessorFactory`; the 8.x `AddLokiBulkDataProcessor(connectionString, assembly)` registration still
works. Settings are per processor, Loki never rolls back a transaction you own, failures keep the driver error as
`InnerException` with fixed, data-free messages, `SaveAsync` takes a `CancellationToken`, and Loki logs through your
host's logging by default. `IBulkProcessor.ConnectionString` and `WithConnectionString` are removed.

**Upgrading from 8.x?** Read the
[upgrade guide](https://github.com/stephen-rebner/loki-bulk-data-processor#upgrade-guide), which starts with a
five-minute checklist and then lists every change with the action it needs.

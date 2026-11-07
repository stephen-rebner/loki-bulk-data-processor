# /implement — Full Agent Pipeline

**Task:** $ARGUMENTS

Run the four stages below in strict sequence. Complete each before starting the next. Do not skip stages.

Use this for higher-risk changes where code and security review should run automatically — new database providers, transaction/connection handling, public API additions, new NuGet dependencies. For lower-risk work `/build` or `/dev` costs less.

| Stage | Agent |
|-------|-------|
| Implementation | `dotnet-senior` |
| Code review | `code-review` |
| Security review | `security-review` |

---

## Stage 1 — Implementation

Spawn `dotnet-senior` with the task and this reminder: methods ≤ ~20 lines; private methods per logical step; provider types only in `InternalDbOperations/`; never commit/dispose a caller-supplied transaction; no breaking public API; tests in the same pass; run `dotnet build` and both test projects and report results honestly.

Afterwards run `git diff --stat` and report what changed. If the diff is empty, skip Stages 2–3 and say so.

---

## Stage 2 — Code Review

Spawn `code-review` with `git diff master...HEAD` and the task description. Output its full findings (Critical / Warning / Suggestion + checklist table).

If any **Critical** findings exist, pause and ask whether to fix them now or proceed and fix in a follow-up.

---

## Stage 3 — Security Review

Spawn `security-review` with the diff, the task description, and whether the change touches connection handling, transactions, identifier/SQL construction, JSON parsing, or dependencies. Output its full findings (Critical / High / Medium / Informational + checklist table).

---

## Stage 4 — Summary

```
## Implementation Summary

### What was built
[bullets]

### Files changed
[git diff --stat]

### Public API impact
[none / additive / breaking]

### Code Review
- Critical: [count] — [titles]
- Warning: [count] — [titles]
- Suggestion: [count] — [titles]

### Security Review
- Critical: [count] — [titles]
- High: [count] — [titles]
- Medium: [count] — [titles]

### Before merging
[ ] Address all Critical and High findings
[ ] dotnet build — zero errors
[ ] dotnet test LokiBulkDataProcessor.UnitTests
[ ] dotnet test LokiBulkDataProcessor.IntegrationTests (Docker)
[ ] No secrets in diff; no test-only packages in the library csproj
[ ] Version / release notes / README upgrade guide updated if shipping behaviour
[ ] CLAUDE.md updated if structure changed
[ ] PR description accurately reflects the changes
```

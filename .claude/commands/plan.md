# /plan — Feature Planning with the Research Agent

**Feature / ticket description:** $ARGUMENTS

> If `$ARGUMENTS` names specific folders/files, pass them to `dotnet-research` as a **starting point** for Round 1 — it should look there first rather than re-deriving the area. This narrows where it starts, never what it may report: it must still flag any file outside that scope it believes the feature needs.

Facilitate an iterative planning session with `dotnet-research`. The goal is a complete, unambiguous implementation plan ready to become a GitHub issue. Do not proceed until all open questions are resolved.

---

## Round structure

### Round 1 — Initial analysis

Spawn `dotnet-research` with the task (and any user-supplied scope). It returns a research report with code samples, file paths, public-API impact, and a list of **open questions**.

### Round 2+ — Question clarification

1. Group the open questions by theme; remove duplicates.
2. Present them to the user as a numbered list. Ask only what cannot be answered from the codebase.
3. Wait for answers.
4. Re-spawn `dotnet-research` with the original task + all answers so far; ask it to revise the plan and return any remaining questions.
5. Repeat until a round returns zero open questions.

### Spike round (optional, user-initiated)

If the user is unsure about something or the agent proposes a spike (e.g. "does `OracleBulkCopy` honour an external `OracleTransaction`?"):

1. Present the proposal: what it demonstrates, roughly how long.
2. If approved, have `dotnet-research` follow its spike branch protocol.
3. Report the branch and what was built; ask whether the concept is now clear.
4. Spike branches are never merged.

---

## Consolidated plan format

```
## Feature Plan: [Feature Name]

### Summary
[2–3 sentences]

### Public API impact
| Member | Change | Breaking? |
|--------|--------|-----------|

### Files to create
| File | Purpose |
|------|---------|

### Files to modify
| File | Change | Approx. lines |
|------|--------|---------------|

### Key code samples
[Class skeletons, DI registration, test base class]

### NuGet changes
[Package, project, version, justification — or "none"]

### Security implications
[Identifier injection, credential exposure, transaction integrity, dependency risk]

### Testing plan
| Layer | What to test | Test type |
|-------|-------------|-----------|
| Guards / mappings / DI | ... | Unit (NUnit + Moq) |
| Provider bulk copy | each input shape, mappings, nulls, internal + external tx, failure rollback | Integration (Testcontainers) |

### Versioning
[Version bump, release notes, README upgrade-guide row]

### Implementation order
1. ...

### Definition of done
- [ ] All listed files created / modified
- [ ] `dotnet build` clean; `dotnet test` passes (unit + integration)
- [ ] No breaking public API change (or declared, with major bump + README row)
- [ ] No secrets committed; no test-only packages in the library csproj
- [ ] README / CLAUDE.md updated if behaviour or structure changed
- [ ] Code review and security review pass
```

---

## GitHub issue creation

Once the user confirms the plan is complete and correct:

```bash
gh issue create \
  --title "[Feature] <short title>" \
  --body "$(cat <<'BODY'
## Summary
<2–3 sentences>

## Implementation plan
<full consolidated plan>

## Definition of done
<DoD checklist>
BODY
)"
```

Report the issue URL.

---

## Rules

- **Never assume.** If a question can't be answered from the codebase, ask.
- **Never merge spike branches.**
- **Never create the issue until the user explicitly confirms the plan.**
- **Always include concrete code samples** — no pseudocode-only plans.
- **Keep asking** until a round returns zero open questions.
- **User-supplied scope is a starting point, not a boundary.**

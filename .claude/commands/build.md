# /build — Plan + Implement, One Approval Gate

**Task:** $ARGUMENTS

> If `$ARGUMENTS` names specific folders/files (e.g. `LokiBulkDataProcessor/InternalDbOperations`), treat that as a **starting point**, not a boundary. Pass it to the agent so it doesn't rediscover what you already told it — but the agent must still name any other file the change needs (a `ServiceCollectionExtension` registration, a csproj package reference, a new integration base class) in the plan. Scope narrows *where to start looking*; it never narrows what shows up in the plan.

Take a feature or bug fix from description to an approved plan, then to an open PR — planning and implementation by the same `dotnet-senior` agent, with **one hard pause** between plan and code. Unlike `/dev`, there is a plan to approve. Unlike `/implement`, review is not automatic — ask for `/review`, `code-review` or `security-review` when you want it.

---

## Autonomy

Run every stage back to back without stopping, **except the one gate** after Stage 3a:

- Create the GitHub issue without asking for sign-off on its wording.
- Pick the branch name and file layout yourself.
- **Do** pause after the plan and wait for explicit approval.
- Once approved, commit, push and open the PR without further confirmation.

Only stop early if you hit something you genuinely cannot resolve (missing tooling, an ambiguous requirement with materially different outcomes, a failing test you cannot explain). Say exactly what you need.

---

## Stage 1 — Ticket

- `$ARGUMENTS` contains `#<n>`, a bare issue number or an issue URL → `gh issue view <n>` and use it as the spec.
- Otherwise create one:

```bash
gh issue create \
  --title "<[Fix] | [Feature]> <short imperative title>" \
  --label "<bug | enhancement>" \
  --body "$(cat <<'BODY'
## Summary
<2–3 sentences: what's wrong / wanted, and why>

## Public API impact
<none / additive / breaking>

## Acceptance criteria
- [ ] <observable outcome 1>
- [ ] <observable outcome 2>
- [ ] Tests cover the change
BODY
)"
```

Capture the issue number and URL.

---

## Stage 2 — Branch

If the session already has a designated feature branch, use it. Otherwise:

```bash
git checkout master
git pull --ff-only origin master
git checkout -b <fix|feat>/<kebab-slug>-<issue-number>
```

---

## Stage 3a — Plan (no files touched)

Spawn `dotnet-senior` with: **plan only, do not write or edit any file.** Pass the full issue body and the original `$ARGUMENTS`. It returns:

- The closest existing implementation it's following.
- Every file to create or modify as a checkbox list:
  `- [ ] LokiBulkDataProcessor/InternalDbOperations/X.cs — new … `
- Files **outside** the given scope, called out explicitly.
- Short code samples for each non-trivial piece (skeletons, not full implementations).
- Public API impact and any NuGet changes.
- Open questions where two reasonable engineers would build materially different things.

Present it as:

```
## Plan: <issue title> (#<n>)

### Library
- [ ] ...

### Tests
- [ ] ...

\`\`\`csharp
// skeleton
\`\`\`

### Public API impact
### NuGet changes
### Open questions   (omit if none)
```

**Stop and wait for the user.** Fold small corrections straight into Stage 3b; re-confirm only for substantial scope changes.

---

## Stage 3b — Implement (after approval)

Re-invoke `dotnet-senior` with the approved plan, the issue body, and this reminder: methods ≤ ~20 lines; private methods per logical step; provider types only in `InternalDbOperations/`; never commit/dispose a caller-supplied transaction; no breaking public API; tests in the same pass.

Afterwards run `git diff --stat` and flag anything the plan didn't mention.

---

## Stage 4 — Verify

```bash
dotnet build
dotnet test LokiBulkDataProcessor.UnitTests
dotnet test LokiBulkDataProcessor.IntegrationTests   # Docker required
```

Loop with the agent until green. If an environment can't run a step (no SDK, no Docker), report that explicitly — never claim it passed.

---

## Review — skipped unless asked

Do not spawn `code-review` or `security-review` unless `$ARGUMENTS` or the user asks. If asked, run them, fix Critical/High findings, and re-run Stage 4.

---

## Stage 5 — PR

```bash
git add <only the files this task intended to change>   # never `git add -A`
git commit    # use this session's commit-trailer convention
git push -u origin HEAD
gh pr create --base master \
  --title "<fix|feat>(<scope>): <short title> (#<n>)" \
  --body "$(cat <<'BODY'
Closes #<n>

## What changed
<bullets>

## Plan
<approved checklist, checked off>

## Public API impact
<none / additive / breaking + version bump>

## Testing
- <command> — <result>

## Review
Not run — use `/review`, `code-review` or `security-review` to add one.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

Report the issue URL and PR URL.

---

## Rules

- **No complexity bounce.** Never refuse a task for being big; stop only for genuine ambiguity.
- **No review by default.**
- **Same agent plans and implements** — no separate research agent.
- **One approval gate, no more.**
- **User-supplied scope is a starting point, not a boundary.**
- **Stay in scope.** No drive-by refactors or reformatting.
- **Stage files explicitly** and sanity-check with `git diff -w --staged` (files carry BOMs/CRLF).
- **One issue, one branch, one PR.** File fan-out work as follow-ups.

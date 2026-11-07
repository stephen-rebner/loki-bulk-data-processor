# /dev — Straight-Through Feature / Bug Fix

**Task:** $ARGUMENTS

Take a single feature or bug fix from description to open PR in one autonomous pass. No planning rounds, no confirmation gates. There is no complexity ceiling.

Use `/plan` for iterative back-and-forth before code. Use `/build` for a plan to approve before implementation.

---

## Autonomy

Run every stage back to back without stopping:

- Create the GitHub issue without sign-off.
- Pick branch name, approach and file layout yourself.
- Auto-fix any **Critical** or **High** review finding, then re-verify.
- Commit, push and open the PR without a final confirmation.

Only stop early for something genuinely unresolvable (missing tooling, materially ambiguous requirement, unexplained failing test). Say exactly what you need.

---

## Stage 1 — Ticket

- `$ARGUMENTS` references an issue → `gh issue view <n>` and use it.
- Otherwise:

```bash
gh issue create \
  --title "<[Fix] | [Feature]> <short imperative title>" \
  --label "<bug | enhancement>" \
  --body "$(cat <<'BODY'
## Summary
<2–3 sentences>

## Public API impact
<none / additive / breaking>

## Acceptance criteria
- [ ] <outcome 1>
- [ ] <outcome 2>
- [ ] Tests cover the change
BODY
)"
```

---

## Stage 2 — Branch

Use the session's designated feature branch if there is one; otherwise:

```bash
git checkout master
git pull --ff-only origin master
git checkout -b <fix|feat>/<kebab-slug>-<issue-number>
```

---

## Stage 3 — Implement

Spawn `dotnet-senior` with the issue body, the original `$ARGUMENTS`, and this reminder: methods ≤ ~20 lines; private methods per logical step; provider types only in `InternalDbOperations/`; never commit/dispose a caller-supplied transaction; no breaking public API; tests in the same pass.

Afterwards run `git diff --stat`.

---

## Stage 4 — Verify

```bash
dotnet build
dotnet test LokiBulkDataProcessor.UnitTests
dotnet test LokiBulkDataProcessor.IntegrationTests   # Docker required
```

Loop until green. Report any step the environment could not run.

---

## Stage 5 — Review

Spawn `code-review` and `security-review` in parallel, each with `git diff master...HEAD` and the issue body.

- **Critical / High** → re-invoke `dotnet-senior` to fix, then re-run Stage 4.
- **Medium / Low / Suggestion** → file a follow-up issue (`gh issue create --label enhancement`) referencing the PR.

---

## Stage 6 — PR

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

## Public API impact
<none / additive / breaking + version bump>

## Testing
- <command> — <result>

## Review
- Code review: <n Critical / n Warning / n Suggestion> — <disposition>
- Security review: <n Critical / n High / n Medium> — <disposition>
- Follow-ups filed: <#nn or "none">

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

Report the issue URL and PR URL.

---

## Rules

- **No complexity bounce**; stop only for genuine ambiguity.
- **No research agent** — `dotnet-research` belongs to `/plan` and `/research`.
- **Autonomous** — no confirmation prompts.
- **Stay in scope**; no drive-by refactors.
- **Stage files explicitly**; check `git diff -w --staged`.
- **One issue, one branch, one PR.**

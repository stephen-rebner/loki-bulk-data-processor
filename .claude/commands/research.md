# /research — Research Agent Analysis

**Task:** $ARGUMENTS

Run the `dotnet-research` agent and return its findings. This is a lightweight read-only pass — no implementation, no GitHub issue. Use it to understand scope and approach before committing to `/build`, `/dev`, `/implement` or `/plan`.

---

## Step 1 — Spawn the research agent

Spawn `dotnet-research` with the task. It must return:

- The closest existing implementation used as a reference (e.g. `InternalDbOperations/BulkCopyCommand.cs`).
- Every file that will be created or modified, with a one-line reason.
- Key code samples (class skeletons, DI registration, test fixture).
- Public API impact (added / changed / removed members, breaking or not).
- NuGet changes (which project, which package, why).
- Security implications (identifier injection, credential exposure, transaction integrity, dependency risk).
- Any open questions that cannot be answered from the codebase.

If the task involves a database provider, the agent should also research the vendor's bulk-copy API from current docs and list behavioural differences from `SqlBulkCopy`.

---

## Step 2 — Present findings

Output the agent's report in full, then a short summary:

```
## Research Summary: [Task]

### Files to create / modify
| File | Reason |
|------|--------|

### Public API impact
[Additive / breaking — and proposed version bump]

### Open questions
[Numbered list. Omit if none.]

### Risks / notes
[Breaking change? New dependency? Security surface? Omit if none.]
```

If there are open questions, present them and wait for answers before declaring research complete. One round of clarification is enough for a research pass — for iterative planning use `/plan`.

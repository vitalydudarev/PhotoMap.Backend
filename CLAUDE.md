# PhotoMap

- Backend: this repo (.NET, `PhotoMap.Backend.sln`).
- Frontend: `/Users/vitaly/Documents/GitHub/PhotoMap.Frontend` (Angular).

## Team agents

Project subagents live in `.claude/agents/`. When the user asks to do something, delegate to these agents, running **only the ones the task actually needs** — never all of them by default.

| Agent | Use when |
|---|---|
| `business-analyst` | The request is vague, user-facing, or spans backend + frontend and needs requirements/acceptance criteria first. |
| `dotnet-developer` | Any backend (C#/.NET) change. |
| `angular-developer` | Any frontend (Angular) change. |
| `code-reviewer` | After code was written by a developer agent, or when the user asks for a review. |
| `qa-tester` | When the change has testable logic worth verifying, or when the user asks to test. |

Routing guidance:
- Pure question / explanation → answer directly, no agents.
- Small, clear backend-only fix → `dotnet-developer` (then `code-reviewer` if non-trivial).
- Small, clear frontend-only fix → `angular-developer` (then `code-reviewer` if non-trivial).
- New feature across both sides → `business-analyst` → `dotnet-developer` (contract first) → `angular-developer` → `code-reviewer` and `qa-tester` (these two can run in parallel).
- "Review this" → `code-reviewer` only. "Test this" → `qa-tester` only.
- Run independent agents in parallel; pass each agent the outputs it depends on (spec, API contract changes).
- If the reviewer or QA reports blockers, send them back to the relevant developer agent, then re-check.
- Tell the user briefly which agents were run and why, and relay their key results.
- Agents never commit or push; the user decides that.

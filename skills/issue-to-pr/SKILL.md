---
name: issue-to-pr
description: "Turns a GitHub issue of the UAI.ZeroVP repository into a tested, review-ready Pull Request without human interaction. Use when the user runs `/issue-to-pr #42`, `/issue-to-pr 42` or `/issue-to-pr <GitHub issue URL>`, or asks to implement an issue end to end (branch, C# code, tests, docs, PR)."
---

# /issue-to-pr – autonomous issue → Pull Request workflow

This skill drives one GitHub issue through a fixed **5-stage state machine**:

```text
[1 Ingestion & pre-flight] -> [2 Planning] -> [3 Layered synthesis] -> [4 Self-healing test loop] -> [5 Docs & PR release]
          |                        |                   |                          |                            |
          +------------------------+-------------------+------------- any FAIL -> [ABORT: report, no PR] <-----+
```

You are the **orchestrator**. You do not write C# or documentation yourself. You run the shell gates
and the commits, pass context between stages and delegate the work to four specialised subagents.
The run is **unattended**: never ask the user for confirmation between stages.
The only way to stop early is the ABORT state described below.

## Files this skill uses

Links in this table are relative to this skill folder (`skills/issue-to-pr/`).
All other paths in this file and all shell commands are relative to the repository root.

| Purpose | File |
| ----- | ----- |
| Architectural rules (always load) | [`rules/dotnet-architecture.md`](rules/dotnet-architecture.md) |
| Git & hygiene rules (always load) | [`rules/git-workflow.md`](rules/git-workflow.md) |
| Planner subagent | [`agents/architect-planner.md`](agents/architect-planner.md) + [`prompts/decompose-issue.md`](prompts/decompose-issue.md) |
| Developer subagent | [`agents/csharp-developer.md`](agents/csharp-developer.md) + [`prompts/csharp-layer-synthesis.md`](prompts/csharp-layer-synthesis.md) |
| Repair subagent | [`agents/qa-repair-agent.md`](agents/qa-repair-agent.md) + [`prompts/compiler-test-reflection.md`](prompts/compiler-test-reflection.md) |
| Release subagent | [`agents/release-agent.md`](agents/release-agent.md) + [`prompts/generate-pr.md`](prompts/generate-pr.md) |

Read both rule files before Stage 1 and pass them to every subagent you invoke.
If any file from the table is missing, ABORT with reason `skill incomplete: <path>`.

## Input

Accept exactly one issue reference and normalise it to a number `<N>`:

| Input form | Example | Parsed `<N>` |
| ----- | ----- | ----- |
| Hash + number | `/issue-to-pr #42` | `42` |
| Bare number | `/issue-to-pr 42` | `42` |
| Issue URL | `/issue-to-pr https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/issues/42` | `42` |

Rules:

- Use the regex `^#?(\d+)$` for the first two forms and `/issues/(\d+)` for the URL form.
- A URL that points to a different repository than `git remote get-url origin` → ABORT (`issue belongs to another repository`).
- A pull request URL (`/pull/<N>`) or no number at all → ABORT (`invalid issue reference`).

## Subagent execution

Each subagent file in `agents/` starts with YAML frontmatter (`name`, `description`, `tools`, `EnableWriteTools`).
That frontmatter is the **single source of truth** for the subagent's tool scope. Never widen it.

| Subagent | Stage | `EnableWriteTools` | Allowed to |
| ----- | ----- | ----- | ----- |
| `architect-planner` | 2 | `false` | read files, search the codebase, run `gh issue view` |
| `csharp-developer` | 3 | `true` | edit `src/`, `tests/`, `README.md`, `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http` |
| `qa-repair-agent` | 4 | `true` | run `dotnet build` / `dotnet test`, edit the files named in diagnostics |
| `release-agent` | 5 | `true` | run Git, `dotnet list package`, `gh pr create` |

Pick the first mode the current runtime supports:

1. **Antigravity (native subagents).** At the start of the run, register every subagent once with
   `define_subagent`: the name and `EnableWriteTools` value come from the agent file's frontmatter,
   and the agent file body is the subagent's system instructions. Then run each stage with
   `invoke_subagent`, passing the filled-in prompt template from `prompts/` as the task.
2. **VS Code GitHub Copilot (agent mode).** Use the subagent tool (`agent/runSubagent`, shown as
   *Run Subagent*). A subagent does not see this conversation, so its prompt must contain, in this order:
   the full agent file, both rule files, the filled-in prompt template and the stage input.
   Copilot cannot enforce `EnableWriteTools: false` for such a subagent, so state the restriction
   explicitly at the top of the prompt: `You are read-only. Do not create, edit or delete any file.`
3. **No subagent support.** Run the stage yourself: load the agent file as your role for that stage,
   follow its tool scope literally, then drop the role before the next stage.

Every subagent call is stateless. Give it all the context it needs and keep its output for the next stage.
Keep stage outputs in the conversation (or in the OS temp directory). Never write run artefacts into the repository.

## Stage 1 – Ingestion & pre-flight

Run these gates in order. Any failure → ABORT.

1. **Clean tree.** `git status --porcelain` must not list tracked changes (lines not starting with `??`).
   Untracked files are tolerated and must never be staged (see `rules/git-workflow.md`).
2. **Tools available.** `dotnet --version` reports 10.x and `gh auth status` succeeds.
3. **Fetch the issue.**

   ```bash
   gh issue view <N> --json number,title,body,labels
   ```

   ABORT if the command fails or the issue is closed (`gh issue view <N> --json state`).
4. **Derive the branch name** `feat/t2-<N>-<short-slug>` as defined in `rules/git-workflow.md`.
5. **Create the branch from fresh `main`.**

   ```bash
   git checkout main
   git pull --ff-only origin main
   git checkout -b feat/t2-<N>-<short-slug>
   ```

   ABORT if the branch already exists locally or on `origin` (`git ls-remote --heads origin <branch>`).
   Never reuse, reset or force-push an existing branch.
6. **Baseline.** Run `dotnet test UAI.ZeroVP.sln --configuration Release` once and record the number of passed tests.
   ABORT if `main` itself is red: the workflow must not mix its own changes with an existing failure.

Output of Stage 1: issue JSON, branch name, baseline test count.

## Stage 2 – Planning (read-only)

Invoke `architect-planner` with `prompts/decompose-issue.md`, the issue JSON and both rule files.
It must not modify anything.

The returned **Execution Plan** must name, per tier, the exact files to create or change:
`DTO` → `Data` → `API` → `UnitTests` → `Docs`. A tier with no changes is listed as `none`.
It must also list the test cases to add and every existing test or mock setup the change will break.

Gate: the plan exists, every file path in it is inside `src/`, `tests/`, `README.md` or
`src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`, and it does not touch `skills/`, `.github/`, `.agents/` or `WORKLOG.md`.
Otherwise re-invoke the planner once with the violation listed. A second invalid plan → ABORT.

## Stage 3 – Layered synthesis

Invoke `csharp-developer` with `prompts/csharp-layer-synthesis.md`, the Execution Plan and both rule files.
Run it **once per tier, strictly in dependency order**:

1. `UAI.ZeroVP.DTO`
2. `UAI.ZeroVP.Data`
3. `UAI.ZeroVP.API`
4. `UAI.ZeroVP.UnitTests`

Skip tiers marked `none`. After each tier run `dotnet build UAI.ZeroVP.sln --configuration Release`.
A build error is handed to Stage 4 and does **not** stop synthesis of later tiers.

After the last tier, create one commit per tier that changed (for example `feat(...)` for code and
`test(...)` for tests) as described in `rules/git-workflow.md`.

## Stage 4 – Self-healing test loop

```bash
dotnet test UAI.ZeroVP.sln --configuration Release
```

The loop passes when **all** of these hold:

- the build has no errors (`error CSxxxx`) and no new warnings compared with `main` (in particular no `CS1591`);
- every test passes;
- the passed-test count is greater than or equal to the baseline plus the tests the plan added.

On failure, invoke `qa-repair-agent` with `prompts/compiler-test-reflection.md`, the full diagnostic output,
the current `git diff main...HEAD` and the attempt number. Then run the test command again.

- At most **4 repair attempts**. Record the attempt count; Stage 5 reports it.
- The repair agent fixes the code under test. It must not delete, skip or weaken an existing test to get green.
- Commit each successful repair as `fix(...)` or `test(...)`.
- Still red after attempt 4 → ABORT. The local branch stays as it is for a human to inspect. Do not push it.

## Stage 5 – Docs & PR release

1. **Docs.** If a public contract changed (route, query or body parameter, status code, DTO field, MCP tool),
   invoke `csharp-developer` once more for the `Docs` tier of the plan, so that `README.md`
   (section *Endpointy* or *MCP Server*) and `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http` describe the change.
   Commit as `docs(...)`.
2. **Hygiene gates** from `rules/git-workflow.md`: `git diff --check main...HEAD`, UTF-8 BOM on every changed `.cs` file,
   NuGet vulnerability scan. Any failure → hand it to `qa-repair-agent` once; still failing → ABORT.
3. **Release.** Invoke `release-agent` with `prompts/generate-pr.md` and: issue number and title,
   the Execution Plan, `git log --oneline main..HEAD`, the final test summary, the baseline count
   and the number of repair attempts used. The release agent pushes the branch and opens the PR:

   ```bash
   git push -u origin feat/t2-<N>-<short-slug>
   gh pr create --base main --head feat/t2-<N>-<short-slug> --title "<title>" --body-file <temp file>
   ```

   The PR body must contain `Closes #<N>`. Never merge the PR: review and merge belong to the team.

Output of Stage 5: the PR URL.

## ABORT

On any failed gate:

1. Stop immediately. Run no later stage.
2. Do not push, do not open a PR and do not delete the branch.
3. Print a short report: the stage, the gate, the exact command and its last 30 lines of output,
   the branch name and the repair attempts used.

## Final report

At the end of a successful run print:

```text
/issue-to-pr #<N> finished
Branch:            feat/t2-<N>-<short-slug>
Pull Request:      <URL>
Tests:             <passed>/<total> (baseline <baseline>)
Repair attempts:   <k>/4
```

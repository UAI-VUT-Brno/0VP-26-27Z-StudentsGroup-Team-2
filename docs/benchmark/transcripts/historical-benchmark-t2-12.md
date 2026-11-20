# Historical Benchmark Transcript – Issues #28 & #29 (`0VP-T2-11/12`)

- **Task:** Issue #52 (`0VP-T2-26`) – Historical closed issue baseline replay (Track A vs. Track B)
- **Date:** 2026-11-20
- **Historical Issues:** [#28](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/issues/28) (`0VP-T2-11: Add user identification to tasks without authentication`) & [#29](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/issues/29) (`0VP-T2-12: Implement "My Tasks" user filtering in GET endpoints`)
- **Clean Baseline Commit:** `9484e7e` (`Merge pull request #24 from UAI-VUT-Brno/feature/repeat-docs`, immediately prior to PR #34)
- **Baseline Test Suite (`9484e7e`):** `dotnet test UAI.ZeroVP.sln --configuration Release` → **105/105 passed** (0 failed, 0 skipped, 1 s)

---

## Track A – Historical Interactive Implementation (PR #34)

- **Author:** `@stepanch284` (interactive development with GitHub Copilot in VS Code; reviewed and merged by `@Castulik`)
- **Resulting PR:** [#34](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/pull/34) (`3e9de15`, `0VP-T2-11/12/13: UserId on tasks and "My Tasks" filtering`)
- **Timeline (GitHub timestamps):**
  - Issues #28/#29 created: `2026-10-25T21:25Z`
  - Commits authored: `cd570d9` (`2026-10-26T20:14:12Z`) → `a067472` (`2026-10-26T20:14:42Z`) → `0ad64e0` (`2026-10-26T20:15:47Z`)
  - PR #34 opened: `2026-10-26T20:16:52Z` (~22 h 52 min elapsed from issue assignment; exact interactive IDE keystroke/prompt counts were not logged during the October session)
  - PR #34 reviewed & merged: `2026-10-27T09:23:59Z`
- **Scope & Verification (`9484e7e..3e9de15`):**
  - **13 files changed**: `+246 insertions / -11 deletions` (`DTO`, `Data`, `API`, `UAI.ZeroVP.API.http`, `TodosControllerTests.cs`, `InMemoryTodoRepositoryTests.cs`, and new `TodoUserTasksTests.cs` at 193 lines).
  - **Test suite on `3e9de15`:** `dotnet test UAI.ZeroVP.sln --configuration Release` → **130/130 passed** (+25 tests over `105` baseline).
  - **Human interventions:** Full interactive developer-driven loop (prompting, manual file edits, breaking-site updates noted in Issue #29's contract section, local build/test verification, PR authoring).

---

## Track B – Unattended `/issue-to-pr` Replay on `9484e7e` (`bench/historical-t2-12`)

- **Setup:** Branched `bench/historical-t2-12` at `9484e7e`, checked out `skills/`, `.agents/`, and `.github/prompts/` from `main` without committing them so the codebase started at the exact pre-PR-#34 state.
- **Runtime:** Antigravity native subagents (`define_subagent` / `invoke_subagent`), .NET SDK `10.0.401`, `verify-and-test.ps1 -BaseRef 9484e7e`.
- **Wall-Clock Timing (Stages 2–4):** Started `22:08:25+01:00` → Verified green at `22:19:35+01:00` (**11 min 10 s** total across planning, 4 synthesis tiers, BOM normalization, and Release test verification; PR push skipped to avoid duplicating historical PR #34).
- **Human Interventions:** `0`

### Stage-by-Stage Execution

1. **Stage 2 – Planning (`architect-planner`, `46e6268f-3f09-4097-9712-435d28bafea0`, ~2 min 01 s):**
   - Inspected `9484e7e` directly and adapted to the historical codebase state: noted under *Open questions* that `TodoMcpTools` and JSON persistence did not exist yet at `9484e7e` and `Seed()` held 4 items (not 18 as in `rules/dotnet-architecture.md` on `main`).
   - Identified all 5 breaking test call/setup sites (`TodosControllerTests.cs:69,72,86,89` and `InMemoryTodoRepositoryTests.cs:265`), the `CS1737` trailing-default `CancellationToken` constraint on `TodosController.GetAll`, and `NormalizeOptional` helper deduplication in `TodoMapper`.
2. **Stage 3 – Layered Synthesis (`csharp-developer`):**
   - **`DTO` (`13b325d0-65d8-44aa-8e32-7efe7dd38037`, ~43 s):** Modified all 4 DTO records (`[StringLength(200)]` + English XML docs + `<example>alice</example>`). Build after tier: **0 errors, 0 warnings**.
   - **`Data` (`e72e71b6-5223-4ae5-a9b2-76cf0f57bc06`, ~1 min 18 s):** Added `UserId` + `Clone()` copy on `TodoItem`, extended `ITodoRepository.GetAllAsync` and `InMemoryTodoRepository.GetAllAsync` (trimmed `userId` filter, 4-item seed preserved). Build after tier: 1 expected `CS1503` in `TodosController.cs(40,64)`.
   - **`API` (`6d203d09-0c36-4e15-87ff-0c080b422c52`, ~1 min 31 s):** Extracted shared `NormalizeOptional` helper in `TodoMapper` and updated `TodosController.GetAll` (`[FromQuery] string? userId = null`, `CancellationToken cancellationToken = default`, `<param name="userId">`). Build after tier: **0 errors in `DTO`/`Data`/`API`, 0 warnings**, and exact **5 expected `CS1503` errors** in `TodosControllerTests.cs` and `InMemoryTodoRepositoryTests.cs`.
   - **`UnitTests` (`fe5891b8-b13a-4a90-917d-0909ec365800`, ~3 min 53 s):** Fixed all 5 breaking sites and generated `tests/UAI.ZeroVP.UnitTests/TodoUserTasksTests.cs` (539 lines, 26 isolated test methods / 38 xUnit test cases).
3. **Stage 4 – Verification (`verify-and-test.ps1 -BaseRef 9484e7e`):**
   - Automatically fixed missing UTF-8 BOM (`EF BB BF`) on **5 of 12** `.cs` files (`TodosController.cs`, `CreateTodoItemRequest.cs`, `TodoItemDto.cs`, `UpdateTodoItemRequest.cs`, `TodoUserTasksTests.cs`).
   - `git diff --check`: clean.
   - `dotnet test UAI.ZeroVP.sln --configuration Release`: **143/143 passed** (`0` failed, `0` skipped, `0` build errors, `0` warnings, `1 s` test runtime).
   - **Self-healing repair attempts used:** `0/4` (`100%` first-attempt pass rate).
4. **Cleanup:** Reset temporary working tree and deleted local branch `bench/historical-t2-12`, restoring clean `main` (`2a9db2c`).

---

## Side-by-Side Comparison (`0VP-T2-11/12` on `9484e7e`)

| Metric | Track A (Interactive PR #34) | Track B (`/issue-to-pr` Replay) |
| ------ | ---------------------------- | ------------------------------- |
| Baseline → Final Tests | `105/105` → `130/130` (`+25`) | `105/105` → `143/143` (`+38` cases across 26 methods) |
| Human Interventions | Full manual cycle (~22h52m issue-to-PR window) | `0` (`11 min 10 s` Stages 2–4) |
| Compiler Errors / Warnings at Stage 4 | `0` at merge (`3e9de15`) | `0` errors, `0` warnings (1st attempt) |
| Self-Healing Attempts Used | N/A (manual debugging) | `0/4` |
| Encoding / Hygiene (`UTF-8 BOM`) | Verified manually | Automated via `verify-and-test.ps1` (5/12 fixed) |

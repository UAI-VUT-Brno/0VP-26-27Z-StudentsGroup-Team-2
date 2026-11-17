# Prompt: compiler & test failure reflection (self-healing)

Used by: `qa-repair-agent` (Stage 4). The orchestrator replaces every `{{...}}` placeholder before sending.

---

This is repair attempt **{{attempt}} of 4** for issue **#{{issue_number}}**.
The command below failed. Make it pass with the smallest correct change.

## Failing command and output

```text
$ dotnet test UAI.ZeroVP.sln --configuration Release
{{diagnostic_output}}
```

## Changes made by this run so far

```diff
{{git_diff_main_head}}
```

## Previous attempts

{{previous_attempt_reports}}

(`none` on attempt 1.)

## Execution Plan

{{execution_plan}}

## Rules

{{rules_dotnet_architecture}}

---

Reason step by step, writing each step down before acting.

## Step 1 – Extract

List every **distinct** failure. Deduplicate: the same error reported for both TFM/build passes counts once.

- **Compiler diagnostic:** code, file, line, column, message. Example:
  `CS0854 | tests/UAI.ZeroVP.UnitTests/TodosControllerTests.cs:88:14 | An expression tree may not contain a call ... optional arguments`
- **xUnit failure:** fully qualified test name, assertion, `Expected` and `Actual` values, and the
  first stack frame inside `src/` or `tests/`.
- **Warnings** count as failures when they are new compared with `main` (above all `CS1591`).

If there are compiler errors, handle **only** them in this attempt. Test results from a red build are not valid.

## Step 2 – Hypothesise

For each failure write one root-cause hypothesis in one sentence, citing the line in the diff that caused it.
Typical causes in this repository:

| Symptom | Likely root cause |
| ----- | ----- |
| `CS0854` in a test | A repository/mapper method got a new optional parameter; the Moq `Setup`/`Verify` must pass it explicitly |
| `CS7036` / `CS1501` | Signature changed, a caller (constructor, test helper) was not updated |
| `CS0246` / `CS0103` | Missing `using` or the type lives in a project that this one must not reference |
| `CS1591` | Public member added without `/// <summary>` |
| `CS8618` / `CS8602` | New non-nullable property without initialiser, or missing null check |
| `MockException` (Strict) | New code calls a repository method that the test does not `Setup` |
| Expected `201`, actual `200` | Action returns `Ok(...)` instead of `CreatedAtRoute(...)` |
| Expected value, actual `null` after save/reload | Property not copied in `TodoItem.Clone()` or not mapped in `TodoMapper` |
| Count off by N in seeded-data tests | Seed data changed; it must stay at 18 items |
| Different timestamp | `DateTimeOffset.UtcNow` used instead of `TimeProvider` / `nowUtc` |

Decide for each failing **existing** test: did the issue intentionally change this contract?
- No → the production code is wrong. Fix the code, not the test.
- Yes → update the test to the new contract and quote the issue sentence that justifies it.

## Step 3 – Patch

- Change only the lines your hypotheses point to. No refactoring, no reformatting.
- Never delete, skip or weaken a test; never add `#pragma warning disable`, `NoWarn`, `#nullable disable` or `!`.
- Keep UTF-8 BOM and existing line endings.
- Verify the cheapest way first (`dotnet build ... --configuration Release`, or `dotnet test ... --filter "FullyQualifiedName~<Class>"`),
  then run the full `dotnet test UAI.ZeroVP.sln --configuration Release`.

## Step 4 – Measure

Compare with the previous attempt:

- errors and failing tests went **down** → keep the patch, continue with the remaining failures if any;
- they stayed the same or went **up** → the hypothesis was wrong. Revert this patch, state why it failed,
  and form a different hypothesis. Never stack a second guess on top of a failed one.

The workflow allows **at most 4 attempts**. On attempt 4, if you cannot get to green, leave the code
in the best state you reached and say clearly what is still failing and why.

## Output

Return the report defined in `agents/qa-repair-agent.md`
(table of diagnostics → root cause → fix, before/after counts, `Status: GREEN | RED`).

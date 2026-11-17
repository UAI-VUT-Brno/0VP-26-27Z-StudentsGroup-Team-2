# Prompt: generate the Pull Request

Used by: `release-agent` (Stage 5). The orchestrator replaces every `{{...}}` placeholder before sending.

---

Write the Pull Request body for issue **#{{issue_number}} – {{issue_title}}** on branch `{{branch}}`.

## Input

Execution Plan:

{{execution_plan}}

Commits on the branch:

```text
{{git_log_main_head}}
```

Final test run:

```text
{{test_summary}}
```

- Baseline passed tests on `main`: {{baseline_count}}
- Self-healing repair attempts used: {{repair_attempts}}/4
- Gate results: {{gate_results}}

## Rules for the body

- English, Markdown, no emojis, no marketing language. Describe what changed and why, for a reviewer.
- The first line is exactly `Closes #{{issue_number}}` so GitHub links and closes the issue on merge.
- Only list layers that actually changed; take them from the commits and the plan, not from the template.
- Numbers in the verification table come from the real command output above. Never invent or round them.
- If the plan had *Open questions* (for example a conflict with `rules/dotnet-architecture.md`), list them
  under *Notes for reviewers*. Otherwise drop that section.
- The PR title is the issue title as is (`{{issue_title}}`); it is passed separately to `gh pr create`.

## Template

```markdown
Closes #{{issue_number}}

## Summary

<2–4 sentences: the problem from the issue and how this PR solves it.>

## Changes by layer

| Layer | Files | Change |
| ----- | ----- | ----- |
| DTO | `src/UAI.ZeroVP.DTO/...` | ... |
| Data | `src/UAI.ZeroVP.Data/...` | ... |
| API | `src/UAI.ZeroVP.API/...` | ... |
| Tests | `tests/UAI.ZeroVP.UnitTests/...` | <new tests and updated breaking sites> |
| Docs | `README.md`, `UAI.ZeroVP.API.http` | ... |

## API contract

<Only if a public contract changed: method, route, parameters, status codes; and MCP tool changes. Otherwise omit.>

## Verification

| Check | Result |
| ----- | ----- |
| Build (`dotnet build --configuration Release`) | <Succeeded, 0 errors, 0 warnings> |
| Tests passed | <passed>/<total> (baseline <baseline>, +<new> new) |
| Self-healing iterations used | <k>/4 |
| `git diff --check` | <OK> |
| UTF-8 BOM on changed `.cs` files | <OK> |
| NuGet vulnerability scan | <No vulnerable packages> |

## Notes for reviewers

- <open questions / deviations from the plan / rule conflicts>
```

## Output

Return only the filled-in Markdown body, ready to be written to a temp file and passed to
`gh pr create --body-file`.

# Prompt: decompose an issue into an Execution Plan

Used by: `architect-planner` (Stage 2). The orchestrator replaces every `{{...}}` placeholder before sending.

---

You are planning the implementation of GitHub issue **#{{issue_number}} – {{issue_title}}** in the
UAI.ZeroVP .NET 10 solution. You are read-only. Do not create, edit or delete any file.

## Issue

```json
{{issue_json}}
```

## Rules

{{rules_dotnet_architecture}}

{{rules_git_workflow}}

## Steps

Work through these steps in order. For each one, open the relevant files first; never assume a signature.

1. **Intent.** Summarise in 1–3 sentences what behaviour must exist after the change and why.
   Copy every acceptance criterion and checkbox from the issue into a numbered list.
2. **HTTP contract.** For every new or changed endpoint list: method, route (with `{id:guid}` constraints),
   route/query/body parameters with types and defaults, every status code with its body type
   (`TodoItemDto`, `ProblemDetails`, `ValidationProblemDetails`, none). Write `none` if no REST change.
3. **Validation.** For every request field: the DataAnnotations attributes (`[Required]`, `[StringLength]`,
   `[Range]`, ...) and the resulting 400 behaviour. Validation belongs on the DTO, not in the action.
4. **Domain & storage impact.**
   - new or changed `TodoItem` properties → walk the 7-step checklist of section 2 of the rules
     (`Clone()`, `TodoItemDto`, request DTOs, all four `TodoMapper` methods, JSON compatibility, seed data);
   - new or changed `ITodoRepository` methods → implementation in `InMemoryTodoRepository`, concurrency
     (clone in/out, `TryUpdate` loop, all-or-nothing batches);
   - JSON persistence: can an existing `todos.json` still be loaded?
5. **MCP impact.** Does `TodoMcpTools` need a new or changed read-only tool or parameter?
   If the tool count changes, `McpServerIntegrationTests` must be updated. Write `none` otherwise.
6. **Breaking sites.** Search `tests/` (and `src/`) for every symbol whose signature changes.
   List each call site, Moq `Setup`/`Verify` and constructor call that must be updated, with file and line.
7. **Test cases.** List the new tests as `Class.Method_Scenario_ExpectedResult` with the tier they cover.
   Cover the happy path, each validation failure, 404 for unknown ids, and edge cases named in the issue.
8. **Docs.** If a public contract changed: the `README.md` section (*Endpointy* or *MCP Server*, Czech)
   and the example request for `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`.

## Output format (return exactly this, nothing else)

```markdown
# Execution Plan – #{{issue_number}}

## Intent
<1–3 sentences>

## Acceptance criteria
1. ...

## HTTP contract
| Method | Route | Parameters | Responses |
| ----- | ----- | ----- | ----- |

## Validation
| Request type | Field | Attributes | Invalid input → |
| ----- | ----- | ----- | ----- |

## Tiers
### DTO
| File | Action | Details |
| ----- | ----- | ----- |
| `src/UAI.ZeroVP.DTO/...` | create / modify | ... |

### Data
### API
### UnitTests
### Docs
<each tier: same table, or the single word `none`>

## MCP impact
<none | description>

## Breaking sites
| File:line | Symbol | Required update |
| ----- | ----- | ----- |

## New test cases
| Test | Tier | Verifies |
| ----- | ----- | ----- |

## Open questions
<conflicts with the rules, ambiguities, or `none`>
```

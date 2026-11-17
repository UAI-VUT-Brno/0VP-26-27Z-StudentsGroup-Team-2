# Prompt: synthesise one tier of C# code

Used by: `csharp-developer` (Stage 3, once per tier; Stage 5 for the `Docs` tier).
The orchestrator replaces every `{{...}}` placeholder before sending.

---

Implement tier **{{tier}}** of the Execution Plan for issue **#{{issue_number}} – {{issue_title}}**.
Change only the files listed for this tier. Earlier tiers are already implemented; later tiers are not.

## Execution Plan

{{execution_plan}}

## Rules

{{rules_dotnet_architecture}}

## Build output of the previous tier

```text
{{previous_build_output}}
```

(Do not fix errors from earlier tiers here – Stage 4 handles them. Only make sure your tier does not depend on them.)

## Order of work

Synthesis is strictly **bottom-up**, following the project references:

```text
DTO  ->  Data  ->  API  ->  UnitTests  ->  Docs
```

Before writing, read every file you are going to change and the files of the tier directly below that you consume.

## Checklist per tier

Go through the list for your tier and tick every item before you return.

### DTO (`src/UAI.ZeroVP.DTO/`)

- [ ] Records/properties added with `/// <summary>` and `<example>` where neighbouring properties use it.
- [ ] Input rules expressed as DataAnnotations on the request DTO (not as `if` checks in a controller).
- [ ] No reference to `Data` or `API`; no internal fields (`UpdatedAtUtc`) exposed in `TodoItemDto`.
- [ ] New optional fields are nullable or have a default, so old clients keep working.

### Data (`src/UAI.ZeroVP.Data/`)

- [ ] New `TodoItem` property has an XML comment **and is copied in `TodoItem.Clone()`**.
- [ ] New `ITodoRepository` method: `async`, `CancellationToken cancellationToken = default` last, XML comment,
      implemented in `InMemoryTodoRepository`.
- [ ] Repository clones on the way in and out; updates use the `TryGetValue` + `TryUpdate` loop.
- [ ] Time from the injected `TimeProvider`; seed data still has exactly 18 items.
- [ ] A `todos.json` written before this change still loads (nullable/default for new properties).

### API (`src/UAI.ZeroVP.API/`)

- [ ] `TodoMapper`: `ToDto`, `ToEntity`, `ToEntities`, `ApplyTo` each handle the new field deliberately; time via `nowUtc`.
- [ ] Controller action: `[ProducesResponseType]` for every status, `<param>` and `<response code>` docs,
      `{id:guid}` route constraint, `CancellationToken` passed to the repository.
- [ ] 404 via `NotFoundTodo(id)`, 201 via `CreatedAtRoute(nameof(GetById), ...)`; never return entities.
- [ ] MCP tool (if planned): `[McpServerTool(Name = "snake_case", ReadOnly = true)]`, `[Description]` on method and every parameter,
      returns `TodoItemDto`, throws `McpException` with the id for a missing task.
- [ ] No `using UAI.ZeroVP.Data.InMemory` in controllers or MCP tools.

### UnitTests (`tests/UAI.ZeroVP.UnitTests/`)

- [ ] Every *breaking site* from the plan is updated: Moq `Setup`/`Verify` pass every argument explicitly
      (expression trees cannot use optional arguments → `CS0854`).
- [ ] New mocks are `MockBehavior.Strict` and have a `Setup` for every repository call the new code makes.
- [ ] Every test from *New test cases* exists, named `Method_Scenario_ExpectedResult`, one behaviour per test.
- [ ] Fixed clock in tests that assert timestamps; MCP tests use the shared `McpTestFixture`.
- [ ] If an MCP tool was added, the tool count in `McpServerIntegrationTests` is updated.
- [ ] No existing test deleted, skipped or weakened.

### Docs (`README.md`, `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`)

- [ ] `README.md` (Czech) section *Endpointy* or *MCP Server* describes the new/changed contract.
- [ ] `.http` file has a runnable example request for every new endpoint.

## Common pitfalls (check them explicitly)

| Pitfall | Symptom | Prevention |
| ----- | ----- | ----- |
| Property not copied in `Clone()` | Value is lost after save/read, test sees `null` | Add the property to `Clone()` in the same edit |
| Moq setup not updated after a signature change | `CS0854` or `MockException` | Update every breaking site listed in the plan |
| Missing XML comment on a public member | `CS1591` warning = failed build | Comment every public type/member you add |
| `DateTimeOffset.UtcNow` in `src/` | Non-deterministic tests | Use `TimeProvider` / `nowUtc` |
| File saved without BOM | Diacritics broken, release gate fails | New `.cs` files start with UTF-8 BOM |
| Wrong status code (`200` instead of `201`/`204`) | Integration assertions fail | Follow the HTTP contract table |

## Output

Return the tier report defined in `agents/csharp-developer.md`.

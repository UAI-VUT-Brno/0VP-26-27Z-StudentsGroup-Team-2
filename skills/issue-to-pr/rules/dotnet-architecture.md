# .NET 10 architecture rules for UAI.ZeroVP

These rules describe how the code in this repository is actually built.
Every subagent that reads or writes C# must follow them. When an issue contradicts a rule,
the issue wins only if it says so explicitly. Otherwise follow the rule and mention the conflict in the PR.

## 1. Projects and dependency direction

| Project | Folder | Contains | May reference |
| ----- | ----- | ----- | ----- |
| `UAI.ZeroVP.DTO` | `src/UAI.ZeroVP.DTO/` | Public request/response records (`TodoItemDto`, `CreateTodoItemRequest`, `UpdateTodoItemRequest`, `RepeatTodoItemRequest`, `InstanceInfoDto`) and the `TodoPriority` enum. DataAnnotations only. | nothing |
| `UAI.ZeroVP.Data` | `src/UAI.ZeroVP.Data/` | Entity `Entities/TodoItem.cs`, contract `Abstractions/ITodoRepository.cs`, implementation `InMemory/InMemoryTodoRepository.cs`, DI registration `DependencyInjection/DataServiceCollectionExtensions.cs` | `DTO` |
| `UAI.ZeroVP.API` | `src/UAI.ZeroVP.API/` | `Program.cs`, `Controllers/`, `Mapping/TodoMapper.cs`, `Mcp/TodoMcpTools.cs`, `Middleware/` | `DTO`, `Data` |
| `UAI.ZeroVP.UnitTests` | `tests/UAI.ZeroVP.UnitTests/` | xUnit tests (Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`) | `API`, `Data` |

The dependency chain is one-way: `DTO` ← `Data` ← `API` ← `UnitTests`. Implementation order follows it: `DTO` → `Data` → `API` → `UnitTests`.

Forbidden:

- a `ProjectReference` from `DTO` to anything, or from `Data` to `API`;
- a new project or a new NuGet package unless the issue asks for it;
- `using UAI.ZeroVP.Data.InMemory` in controllers or MCP tools. They talk to the store only through `ITodoRepository`.

Constructor dependencies currently in use:

| Class | Injected services |
| ----- | ----- |
| `TodosController` | `ITodoRepository`, `TimeProvider`, `ILogger<TodosController>` |
| `InfoController` | `ITodoRepository`, `IHostEnvironment`, `TimeProvider`, `IConfiguration?` |
| `TodoMcpTools` | `ITodoRepository` |

Do not add other dependencies to these classes unless the issue requires them.
Services are registered in `Program.cs` (`TimeProvider.System` as a singleton) and in `AddInMemoryTodoData`.
The repository is a **singleton** on purpose because the data lives in the instance. Never change it to scoped or transient.

## 2. Adding or changing a `TodoItem` property

A new field must flow through every layer. Missing one step compiles fine and silently loses data,
so check the whole list:

1. `TodoItem` in `src/UAI.ZeroVP.Data/Entities/TodoItem.cs`: add the property with an XML doc comment.
2. `TodoItem.Clone()`: copy the new property. The repository stores and returns clones, so a property missing here is lost on every read and write.
3. `TodoItemDto`: add it if clients should see it. Keep internal fields such as `UpdatedAtUtc` out of the DTO.
4. Request DTOs (`CreateTodoItemRequest`, `UpdateTodoItemRequest`, `RepeatTodoItemRequest`): add it where clients may set it, with DataAnnotations validation.
5. `TodoMapper` in `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs`:
   - `ToDto` (entity → DTO),
   - `ToEntity` (`CreateTodoItemRequest` → entity),
   - `ToEntities` (`RepeatTodoItemRequest` → weekly series),
   - `ApplyTo` (`UpdateTodoItemRequest` → existing entity).

   Decide for each method whether the field is set, copied or left alone, and test that decision.
6. JSON persistence (`Todo:StorageFilePath`): `TodoItem` is serialised as is. A new property must be nullable or have a safe default, so older `todos.json` files without it still load.
7. Sample data (`InMemoryTodoRepository.Seed()`): do not add or remove seeded tasks.
   Tests rely on exactly **18** seeded items (`SeededItemCount` in two test classes, plus `McpServerIntegrationTests`).
   Give existing seeded items a value for the new field only if the issue needs it.

## 3. Time

- Controllers take the current time from `_timeProvider.GetUtcNow()`. Never use `DateTimeOffset.UtcNow` or `DateTime.Now` in `src/`.
- `TodoMapper` methods receive the time as a `DateTimeOffset nowUtc` parameter. They never read a clock.
- `InMemoryTodoRepository` uses its injected `TimeProvider`.
- Tests pass a fixed clock (for example the private `FixedTimeProvider` in `TodosControllerTests`) and assert exact timestamps.

## 4. XML documentation (`CS1591`)

All three `src/` projects set `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
The XML comments feed the OpenAPI document and Swagger UI.

- Every public type, constructor, method, property and enum member needs `/// <summary>`.
- Controller actions also need `<param>` for each parameter and `<response code="...">` for each status code.
- DTO properties use `<example>` where the existing ones do.
- A missing comment shows up as warning `CS1591`. The solution builds with **zero warnings** today, so a new warning counts as a failed build in Stage 4.
- Write new comments in English. Do not translate or rewrite existing Czech comments.

## 5. REST controllers

Follow `src/UAI.ZeroVP.API/Controllers/TodosController.cs`:

- `[ApiController]`, `[Route("api/todos")]`, `[Produces("application/json")]`, `sealed` class.
- Route ids use the constraint `{id:guid}`.
- Each action declares every status it can return with `[ProducesResponseType]`.
- Accept `CancellationToken cancellationToken` as the last parameter and pass it to the repository.
- Map with the `TodoMapper` extension methods. Never return entities.
- `POST` that creates one resource returns `CreatedAtRoute(nameof(GetById), new { id }, dto)`.
- Errors use RFC 9457 problem responses. `Program.cs` already calls `AddProblemDetails()`, `UseExceptionHandler()` and `UseStatusCodePages()`.
  - 404: reuse the private helper `NotFoundTodo(id)`, which returns `Problem(...)` → `ProblemDetails`.
  - 400: validation is automatic through `[ApiController]` and DataAnnotations → `ValidationProblemDetails`. Put input rules on the DTO, not in `if` checks in the action.
- Enums are serialised as strings (`JsonStringEnumConverter` in `Program.cs`).

## 6. MCP tools

Follow `src/UAI.ZeroVP.API/Mcp/TodoMcpTools.cs`. The MCP server at `/mcp` is **read-only by design** and has no authentication.

- The class carries `[McpServerToolType]` and is registered with `.WithTools<TodoMcpTools>()` in `Program.cs`.
- Each tool is `[McpServerTool(Name = "snake_case_name", ReadOnly = true)]` with a `[Description]` on the method and on every parameter.
- Tools return the same DTOs as the REST API (`TodoItemDto`), never entities.
- A missing task throws `McpException` with a message that contains the id. The client then receives a result with `IsError = true`.
- Do not add a tool that writes data unless the issue explicitly asks for one.
- `McpServerIntegrationTests` asserts the exact number of published tools. Adding a tool means updating that test.

## 7. Repository and concurrency

- `ITodoRepository` methods are asynchronous and take `CancellationToken cancellationToken = default` as the last parameter.
- `InMemoryTodoRepository` keeps items in a `ConcurrentDictionary<Guid, TodoItem>`.
  It **clones on the way in and on the way out**, so callers never hold a reference to stored state.
- Keep the `UpdateAsync` loop: `TryGetValue` + `TryUpdate(id, replacement, existing)` retried until it succeeds or the item disappears. It is last-writer-wins.
- `AddRangeAsync` is all-or-nothing. On a duplicate id it removes everything the batch already added.
- Optimistic concurrency (for example a `version` token and `409 Conflict`) is added only when an issue specifies it.
  Then implement it inside the same `TryUpdate` compare-and-swap and return a `ProblemDetails` 409.
- A new method on `ITodoRepository` must be implemented in `InMemoryTodoRepository` and covered in `InMemoryTodoRepositoryTests`.

## 8. Tests

- Controller and MCP tool tests mock `ITodoRepository` with `new Mock<ITodoRepository>(MockBehavior.Strict)`.
  Every repository call the new code makes needs a matching `Setup`, otherwise the test throws `MockException`.
- Moq setups are expression trees and cannot use optional arguments. When a parameter is added to an
  `ITodoRepository` method, every existing `Setup`/`Verify` of that method must pass the new argument explicitly
  (for example `r.GetAllAsync(null, null, It.IsAny<CancellationToken>())`). Otherwise the build fails with `CS0854`.
  Search all of `tests/` for the method name before changing a signature.
- Repository tests use a real `InMemoryTodoRepository` (`SeedSampleData = false` unless the test needs the sample data).
- MCP end-to-end tests use the shared `McpTestFixture` (Kestrel on a dynamic port). Do not create a second fixture type.
- Name tests `Method_Scenario_ExpectedResult` and keep one behaviour per test.
- Never delete, skip (`Skip =`) or weaken an existing assertion to make the suite pass.

## 9. Files and encoding

- `.cs` files are UTF-8 **with BOM** (`.editorconfig`: `charset = utf-8-bom`). Without the BOM the compiler on Windows reads Czech diacritics with the ANSI code page.
- Indent with 4 spaces, 2 for `.json`, `.yml` and `.csproj`. Trim trailing whitespace and end files with a newline.
- `ImplicitUsings` and `Nullable` are enabled in all projects. Do not add `#nullable disable` or `!` suppressions to silence warnings.
- `README.md` is written in Czech. Document new endpoints in its *Endpointy* section and add an example request to `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`.

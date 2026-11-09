using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Mcp;

/// <summary>
/// Read-only MCP tools over the task list, exposed at <c>/mcp</c>.
/// Like the REST controller, they only know <see cref="ITodoRepository"/> and return the same DTOs.
/// No authentication – intended for local/development use only.
/// </summary>
[McpServerToolType]
public sealed class TodoMcpTools
{
    private readonly ITodoRepository _repository;

    /// <summary>Creates the tools with the repository from the DI container.</summary>
    public TodoMcpTools(ITodoRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Returns tasks, optionally filtered by completion status and user.</summary>
    [McpServerTool(Name = "list_todos", ReadOnly = true)]
    [Description("Lists ToDo tasks sorted by priority and creation time. Optionally filters by completion status and by the user the task belongs to.")]
    public async Task<IReadOnlyList<TodoItemDto>> ListTodos(
        [Description("true = only completed tasks, false = only open tasks; omit for all tasks.")] bool? isCompleted = null,
        [Description("Return only tasks of this user (e.g. \"alice\"); omit for tasks of all users.")] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(isCompleted, userId, cancellationToken);

        return items.Select(item => item.ToDto()).ToList();
    }

    /// <summary>Returns one task by its identifier.</summary>
    /// <exception cref="McpException">The task does not exist – the client receives an error result with this message.</exception>
    [McpServerTool(Name = "get_todo", ReadOnly = true)]
    [Description("Gets the details of a single ToDo task by its ID. Returns an error if no task with this ID exists.")]
    public async Task<TodoItemDto> GetTodo(
        [Description("Unique identifier (GUID) of the task.")] Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);

        return item?.ToDto() ?? throw new McpException($"Task with ID '{id}' was not found.");
    }
}

using ModelContextProtocol;
using Moq;
using UAI.ZeroVP.API.Mcp;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Tests of the MCP tools called directly (without the MCP protocol) – the repository is
/// replaced by a <see cref="Mock{T}"/> of <see cref="ITodoRepository"/>. Verifies that filters
/// are passed to the repository unchanged and results are mapped to DTOs.
/// </summary>
public sealed class TodoMcpToolsTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITodoRepository> _repository = new(MockBehavior.Strict);
    private readonly TodoMcpTools _tools;

    public TodoMcpToolsTests()
    {
        _tools = new TodoMcpTools(_repository.Object);
    }

    private static TodoItem CreateEntity(
        string title,
        bool isCompleted = false,
        string? userId = null,
        string? project = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = isCompleted,
        CreatedAtUtc = CreatedAt,
        CompletedAtUtc = isCompleted ? CreatedAt : null,
        UserId = userId,
        Project = project,
        UpdatedAtUtc = CreatedAt
    };

    // --- list_todos -------------------------------------------------------------

    [Fact]
    public async Task ListTodos_WithoutFilters_ReturnsAllItemsMapped()
    {
        var items = new[] { CreateEntity("First", userId: "alice"), CreateEntity("Second", isCompleted: true) };
        _repository
            .Setup(r => r.GetAllAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.ListTodos();

        Assert.Equal(items.Select(i => i.Id), result.Select(d => d.Id));
        Assert.Equal(["First", "Second"], result.Select(d => d.Title));
        Assert.Equal([false, true], result.Select(d => d.IsCompleted));
        Assert.Equal(["alice", null], result.Select(d => d.UserId));
        _repository.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListTodos_FilteredByIsCompleted_PassesFilterToRepository(bool isCompleted)
    {
        var items = new[] { CreateEntity("Task", isCompleted) };
        _repository
            .Setup(r => r.GetAllAsync(isCompleted, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.ListTodos(isCompleted: isCompleted);

        var dto = Assert.Single(result);
        Assert.Equal(items[0].Id, dto.Id);
        Assert.Equal(isCompleted, dto.IsCompleted);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task ListTodos_FilteredByUserId_PassesFilterToRepository()
    {
        var items = new[] { CreateEntity("Alice's task", userId: "alice") };
        _repository
            .Setup(r => r.GetAllAsync(null, "alice", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.ListTodos(userId: "alice");

        var dto = Assert.Single(result);
        Assert.Equal("alice", dto.UserId);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task ListTodos_FilteredByProject_PassesFilterToRepository()
    {
        var items = new[] { CreateEntity("Project task", project: "ZeroVP") };
        _repository
            .Setup(r => r.GetAllAsync(null, null, "ZeroVP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.ListTodos(project: "ZeroVP");

        var dto = Assert.Single(result);
        Assert.Equal(items[0].Id, dto.Id);
        Assert.Equal("ZeroVP", dto.Project);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task ListTodos_FilteredByAllParameters_PassesAllFiltersToRepository()
    {
        using var cts = new CancellationTokenSource();
        var items = new[] { CreateEntity("Filtered task", isCompleted: true, userId: "alice", project: "ZeroVP") };
        _repository
            .Setup(r => r.GetAllAsync(true, "alice", "ZeroVP", cts.Token))
            .ReturnsAsync(items);

        var result = await _tools.ListTodos(isCompleted: true, userId: "alice", project: "ZeroVP", cancellationToken: cts.Token);

        var dto = Assert.Single(result);
        Assert.Equal(items[0].Id, dto.Id);
        Assert.True(dto.IsCompleted);
        Assert.Equal("alice", dto.UserId);
        Assert.Equal("ZeroVP", dto.Project);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task ListTodos_EmptyRepository_ReturnsEmptyList()
    {
        _repository
            .Setup(r => r.GetAllAsync(false, "bob", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TodoItem>());

        var result = await _tools.ListTodos(isCompleted: false, userId: "bob");

        Assert.Empty(result);
        _repository.VerifyAll();
    }

    // --- get_todo ---------------------------------------------------------------

    [Fact]
    public async Task GetTodo_ExistingId_ReturnsMappedItem()
    {
        var entity = CreateEntity("Found", isCompleted: true, userId: "alice");
        entity.Description = "Details";
        entity.Priority = TodoPriority.High;
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _tools.GetTodo(entity.Id);

        Assert.Equal(entity.Id, result.Id);
        Assert.Equal("Found", result.Title);
        Assert.Equal("Details", result.Description);
        Assert.Equal(TodoPriority.High, result.Priority);
        Assert.True(result.IsCompleted);
        Assert.Equal(CreatedAt, result.CompletedAtUtc);
        Assert.Equal("alice", result.UserId);
        _repository.VerifyAll();
    }

    [Theory]
    [InlineData("ZeroVP")]
    [InlineData(null)]
    public async Task GetTodo_ExistingId_IncludesProjectInDto(string? project)
    {
        var entity = CreateEntity("Task with project", project: project);
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _tools.GetTodo(entity.Id);

        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(project, result.Project);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetTodo_NonExistingId_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItem?)null);

        var exception = await Assert.ThrowsAsync<McpException>(() => _tools.GetTodo(id));

        Assert.Contains(id.ToString(), exception.Message);
        _repository.VerifyAll();
    }
}

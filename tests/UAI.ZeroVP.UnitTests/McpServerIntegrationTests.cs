using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using UAI.ZeroVP.DTO;
using Xunit;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// End-to-end integration tests verifying MCP protocol compliance against the running API.
/// Connects over HTTP/SSE, verifies tool discovery, calls tools with and without filters,
/// and verifies individual task lookup and error handling.
/// </summary>
public sealed class McpServerIntegrationTests : IClassFixture<McpTestFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly McpTestFixture _fixture;

    public McpServerIntegrationTests(McpTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static T DeserializeToolResult<T>(CallToolResult result)
    {
        Assert.NotNull(result);
        var contentSummary = string.Join("; ", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        Assert.True(result.IsError != true, $"Tool execution failed: {contentSummary}");

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var deserialized = JsonSerializer.Deserialize<T>(textContent.Text, JsonOptions);
        Assert.NotNull(deserialized);

        return deserialized;
    }

    [Fact]
    public async Task ListTools_ExposesListTodosAndGetTodoWithDescriptions()
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var tools = await client.ListToolsAsync();

        Assert.Equal(2, tools.Count);

        var listTool = Assert.Single(tools, t => t.Name == "list_todos");
        Assert.False(string.IsNullOrWhiteSpace(listTool.Description));

        var getTool = Assert.Single(tools, t => t.Name == "get_todo");
        Assert.False(string.IsNullOrWhiteSpace(getTool.Description));
    }

    [Fact]
    public async Task ListTodos_WithoutFilters_ReturnsFullSeededDataset()
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var result = await client.CallToolAsync("list_todos");
        var tasks = DeserializeToolResult<List<TodoItemDto>>(result);

        // Seed() creates 18 tasks (4 unassigned + 5 alice + 5 bob + 4 charlie)
        Assert.Equal(18, tasks.Count);
        Assert.Contains(tasks, t => t.UserId is null);
        Assert.Contains(tasks, t => t.UserId == "alice");
        Assert.Contains(tasks, t => t.UserId == "bob");
        Assert.Contains(tasks, t => t.UserId == "charlie");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListTodos_FilteredByIsCompleted_ReturnsOnlyMatchingTasks(bool isCompleted)
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var arguments = new Dictionary<string, object?> { ["isCompleted"] = isCompleted };
        var result = await client.CallToolAsync("list_todos", arguments);
        var tasks = DeserializeToolResult<List<TodoItemDto>>(result);

        Assert.NotEmpty(tasks);
        Assert.All(tasks, t => Assert.Equal(isCompleted, t.IsCompleted));
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("bob")]
    [InlineData("charlie")]
    public async Task ListTodos_FilteredByUserId_ReturnsOnlyUserTasks(string userId)
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var arguments = new Dictionary<string, object?> { ["userId"] = userId };
        var result = await client.CallToolAsync("list_todos", arguments);
        var tasks = DeserializeToolResult<List<TodoItemDto>>(result);

        Assert.NotEmpty(tasks);
        Assert.All(tasks, t => Assert.Equal(userId, t.UserId));
    }

    [Fact]
    public async Task ListTodos_FilteredByBoth_ReturnsMatchingSubset()
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var arguments = new Dictionary<string, object?>
        {
            ["isCompleted"] = false,
            ["userId"] = "alice"
        };

        var result = await client.CallToolAsync("list_todos", arguments);
        var tasks = DeserializeToolResult<List<TodoItemDto>>(result);

        Assert.NotEmpty(tasks);
        Assert.All(tasks, t =>
        {
            Assert.False(t.IsCompleted);
            Assert.Equal("alice", t.UserId);
        });
    }

    [Fact]
    public async Task GetTodo_ExistingId_ReturnsExactTaskDetails()
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        // 1. Retrieve seeded tasks to pick a known ID
        var listResult = await client.CallToolAsync("list_todos");
        var allTasks = DeserializeToolResult<List<TodoItemDto>>(listResult);
        var expectedTask = allTasks.First(t => t.UserId == "alice");

        // 2. Fetch that specific task by ID via get_todo
        var getArguments = new Dictionary<string, object?> { ["id"] = expectedTask.Id };
        var getResult = await client.CallToolAsync("get_todo", getArguments);
        var retrievedTask = DeserializeToolResult<TodoItemDto>(getResult);

        Assert.Equal(expectedTask.Id, retrievedTask.Id);
        Assert.Equal(expectedTask.Title, retrievedTask.Title);
        Assert.Equal(expectedTask.Description, retrievedTask.Description);
        Assert.Equal(expectedTask.Priority, retrievedTask.Priority);
        Assert.Equal(expectedTask.IsCompleted, retrievedTask.IsCompleted);
        Assert.Equal(expectedTask.UserId, retrievedTask.UserId);
        Assert.Equal(expectedTask.DueAtUtc, retrievedTask.DueAtUtc);
    }

    [Fact]
    public async Task GetTodo_NonExistingId_ReturnsErrorResult()
    {
        await using var client = await _fixture.CreateMcpClientAsync();

        var nonExistingId = Guid.NewGuid();
        var arguments = new Dictionary<string, object?> { ["id"] = nonExistingId };

        var result = await client.CallToolAsync("get_todo", arguments);

        Assert.True(result.IsError == true, "Expected get_todo for non-existing task to return IsError = true.");
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(nonExistingId.ToString(), textContent.Text);
    }
}

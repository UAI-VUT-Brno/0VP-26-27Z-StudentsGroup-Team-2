using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using Xunit;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Lifecycle tests for <see cref="McpTestFixture"/> itself.
/// Each test creates its own fixture so the shared seeded data used by
/// <see cref="McpServerIntegrationTests"/> is never modified.
/// </summary>
public sealed class McpTestFixtureTests
{
    [Fact]
    public async Task Services_AndMcpEndpoint_ShareTheSameRepository()
    {
        await using var fixture = new McpTestFixture();
        await using var client = await fixture.CreateMcpClientAsync();

        // Write through the factory's service provider ...
        var repository = fixture.Services.GetRequiredService<ITodoRepository>();
        var added = await repository.AddAsync(new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Written via fixture.Services",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        // ... and read it back over HTTP. With two hosts these would be two separate repositories.
        var result = await client.CallToolAsync("get_todo", new Dictionary<string, object?> { ["id"] = added.Id });

        Assert.True(result.IsError != true, "get_todo did not find the task, so the MCP endpoint uses a different host.");
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(added.Id.ToString(), text);
    }

    [Fact]
    public async Task DisposeAsync_StopsTheKestrelServer()
    {
        var fixture = new McpTestFixture();
        await using (await fixture.CreateMcpClientAsync())
        {
        }

        var address = fixture.ServerAddress;
        Assert.StartsWith("http://127.0.0.1:", address);

        await fixture.DisposeAsync();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync($"{address}/mcp"));
    }
}

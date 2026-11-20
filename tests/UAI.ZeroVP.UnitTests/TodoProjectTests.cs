using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Tests for the optional <c>Project</c> tag on ToDo tasks:
/// DTO validation, entity cloning, <see cref="TodoMapper"/> normalization,
/// case-insensitive repository filtering, and controller parameter forwarding.
/// </summary>
public sealed class TodoProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static TodoItem CreateItem(
        string? userId = null,
        string? project = null,
        bool isCompleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Task",
        IsCompleted = isCompleted,
        UserId = userId,
        Project = project,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now
    };

    // --- DTO validation -------------------------------------------------------

    public static TheoryData<string?, bool> ProjectValidationCases => new()
    {
        { null, true },
        { "   ", true },
        { new string('p', 50), true },
        { new string('p', 51), false }
    };

    [Theory]
    [MemberData(nameof(ProjectValidationCases))]
    public void Requests_ValidateProjectMaxLength(string? project, bool expectedValid)
    {
        object[] requests =
        [
            new CreateTodoItemRequest { Title = "Task", Project = project },
            new UpdateTodoItemRequest { Title = "Task", Project = project },
            new RepeatTodoItemRequest { Title = "Task", FirstDueAtUtc = Now, RepeatCount = 1, Project = project }
        ];

        foreach (var request in requests)
        {
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                results,
                validateAllProperties: true);

            Assert.Equal(expectedValid, isValid);
            if (!expectedValid)
            {
                var error = Assert.Single(results);
                Assert.Contains(nameof(CreateTodoItemRequest.Project), error.MemberNames);
            }
        }
    }

    // --- Entity cloning -------------------------------------------------------

    [Theory]
    [InlineData("ZeroVP")]
    [InlineData(null)]
    public void Clone_CopiesProject(string? project)
    {
        var original = CreateItem(project: project);

        var copy = original.Clone();

        Assert.NotSame(original, copy);
        Assert.Equal(project, copy.Project);

        copy.Project = "Modified";
        Assert.Equal(project, original.Project);
    }

    // --- Repository filtering -------------------------------------------------

    private static async Task<InMemoryTodoRepository> CreateRepositoryAsync()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = false });
        await repository.AddAsync(CreateItem(userId: "alice", project: "ZeroVP", isCompleted: false));
        await repository.AddAsync(CreateItem(userId: "alice", project: "  zerovp  ", isCompleted: true));
        await repository.AddAsync(CreateItem(userId: "bob", project: "ZeroVP", isCompleted: false));
        await repository.AddAsync(CreateItem(userId: "alice", project: "Backend", isCompleted: false));
        await repository.AddAsync(CreateItem(userId: "bob", project: null, isCompleted: false));
        return repository;
    }

    [Theory]
    [InlineData("ZeroVP", 3)]
    [InlineData("zerovp", 3)]
    [InlineData("  ZEROVP  ", 3)]
    [InlineData("backend", 1)]
    [InlineData("nonexistent", 0)]
    public async Task GetAllAsync_FiltersByProject_CaseInsensitiveAndTrimmed(string project, int expectedCount)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(project: project);

        Assert.Equal(expectedCount, items.Count);
        Assert.All(items, i => Assert.Equal(project.Trim(), i.Project?.Trim(), StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllAsync_NullOrWhitespaceProject_ReturnsAllProjects(string? project)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(project: project);

        Assert.Equal(5, items.Count);
        Assert.Contains(items, i => i.Project is null);
    }

    [Fact]
    public async Task GetAllAsync_CombinesIsCompletedUserIdAndProjectFilters()
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(isCompleted: false, userId: "alice", project: "  zerovp ");

        var match = Assert.Single(items);
        Assert.False(match.IsCompleted);
        Assert.Equal("alice", match.UserId);
        Assert.Equal("ZeroVP", match.Project);
    }

    // --- Mapping --------------------------------------------------------------

    [Theory]
    [InlineData("ZeroVP", "ZeroVP")]
    [InlineData("  ZeroVP  ", "ZeroVP")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CreateRequest_ToEntity_TrimsProject_OrStoresNull(string? input, string? expected)
    {
        var entity = new CreateTodoItemRequest { Title = "Task", Project = input }.ToEntity(Now);

        Assert.Equal(expected, entity.Project);
    }

    [Theory]
    [InlineData("  ZeroVP  ", "ZeroVP")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void RepeatRequest_ToEntities_AssignsTrimmedProjectToAllOccurrences(string? input, string? expected)
    {
        var request = new RepeatTodoItemRequest
        {
            Title = "Weekly report",
            FirstDueAtUtc = Now,
            RepeatCount = 3,
            Project = input
        };

        var entities = request.ToEntities(Now);

        Assert.Equal(4, entities.Count);
        Assert.All(entities, e => Assert.Equal(expected, e.Project));
    }

    [Theory]
    [InlineData("  NewProject  ", "NewProject")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void UpdateRequest_ApplyTo_OverwritesProject(string? input, string? expected)
    {
        var entity = CreateItem(project: "OriginalProject");

        new UpdateTodoItemRequest { Title = "Task", Project = input }.ApplyTo(entity, Now);

        Assert.Equal(expected, entity.Project);
    }

    [Theory]
    [InlineData("ZeroVP")]
    [InlineData(null)]
    public void ToDto_MapsProject(string? project)
    {
        Assert.Equal(project, CreateItem(project: project).ToDto().Project);
    }

    // --- Controller -----------------------------------------------------------

    [Fact]
    public async Task Controller_GetAll_PassesProjectToRepository_AndReturnsFilteredDtos()
    {
        using var cts = new CancellationTokenSource();
        var items = new[]
        {
            CreateItem(userId: "alice", project: "ZeroVP"),
            CreateItem(userId: "alice", project: "ZeroVP")
        };
        var repository = new Mock<ITodoRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.GetAllAsync(false, "alice", "ZeroVP", cts.Token))
            .ReturnsAsync(items);
        var controller = new TodosController(
            repository.Object,
            new FixedTimeProvider(Now),
            NullLogger<TodosController>.Instance);

        var result = await controller.GetAll(false, "alice", "ZeroVP", cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(ok.Value);
        Assert.Equal(items.Select(i => i.Id), dtos.Select(d => d.Id));
        Assert.All(dtos, d => Assert.Equal("ZeroVP", d.Project));
        repository.VerifyAll();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

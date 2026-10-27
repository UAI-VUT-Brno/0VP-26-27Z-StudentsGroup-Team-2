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
/// Testy identifikace uživatele u úkolů (<c>UserId</c>) a filtru „Moje úkoly“:
/// mapování a ořezávání v <see cref="TodoMapper"/>, validace DTO,
/// filtrování v <see cref="InMemoryTodoRepository"/> a předání parametru kontrolerem.
/// </summary>
public sealed class TodoUserTasksTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static TodoItem CreateItem(string? userId = null, bool isCompleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Úkol",
        IsCompleted = isCompleted,
        UserId = userId,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now
    };

    // --- Mapování -----------------------------------------------------------------

    [Theory]
    [InlineData("jan.novak", "jan.novak")]
    [InlineData("  jan.novak  ", "jan.novak")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CreateRequest_ToEntity_TrimsUserId_OrStoresNull(string? input, string? expected)
    {
        var entity = new CreateTodoItemRequest { Title = "Úkol", UserId = input }.ToEntity(Now);

        Assert.Equal(expected, entity.UserId);
    }

    [Fact]
    public void RepeatRequest_ToEntities_AssignsTrimmedUserIdToAllOccurrences()
    {
        var request = new RepeatTodoItemRequest
        {
            Title = "Týdenní report",
            FirstDueAtUtc = Now,
            RepeatCount = 3,
            UserId = " jan.novak "
        };

        var entities = request.ToEntities(Now);

        Assert.Equal(4, entities.Count);
        Assert.All(entities, e => Assert.Equal("jan.novak", e.UserId));
    }

    [Fact]
    public void RepeatRequest_ToEntities_WithoutUserId_LeavesNull()
    {
        var request = new RepeatTodoItemRequest { Title = "Report", FirstDueAtUtc = Now, RepeatCount = 1 };

        Assert.All(request.ToEntities(Now), e => Assert.Null(e.UserId));
    }

    [Theory]
    [InlineData(" petr ", "petr")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void UpdateRequest_ApplyTo_OverwritesUserId(string? input, string? expected)
    {
        var entity = CreateItem(userId: "puvodni");

        new UpdateTodoItemRequest { Title = "Úkol", UserId = input }.ApplyTo(entity, Now);

        Assert.Equal(expected, entity.UserId);
    }

    [Fact]
    public void Clone_CopiesUserId()
    {
        Assert.Equal("jan.novak", CreateItem(userId: "jan.novak").Clone().UserId);
    }

    [Theory]
    [InlineData("jan.novak")]
    [InlineData(null)]
    public void ToDto_MapsUserId(string? userId)
    {
        Assert.Equal(userId, CreateItem(userId).ToDto().UserId);
    }

    // --- Validace DTO -------------------------------------------------------------

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Requests_ValidateUserIdMaxLength(int length, bool expectedValid)
    {
        var userId = new string('u', length);
        object[] requests =
        [
            new CreateTodoItemRequest { Title = "Úkol", UserId = userId },
            new UpdateTodoItemRequest { Title = "Úkol", UserId = userId },
            new RepeatTodoItemRequest { Title = "Úkol", FirstDueAtUtc = Now, RepeatCount = 1, UserId = userId }
        ];

        foreach (var request in requests)
        {
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
            Assert.Equal(expectedValid, isValid);
        }
    }

    // --- Repozitář ----------------------------------------------------------------

    private static async Task<InMemoryTodoRepository> CreateRepositoryAsync()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = false });
        await repository.AddAsync(CreateItem("alice"));
        await repository.AddAsync(CreateItem("alice", isCompleted: true));
        await repository.AddAsync(CreateItem("bob"));
        await repository.AddAsync(CreateItem(null));
        return repository;
    }

    [Theory]
    [InlineData("alice", 2)]
    [InlineData("  alice ", 2)]
    [InlineData("bob", 1)]
    [InlineData("nikdo", 0)]
    public async Task GetAllAsync_FiltersByUserId(string userId, int expectedCount)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(userId: userId);

        Assert.Equal(expectedCount, items.Count);
        Assert.All(items, i => Assert.Equal(userId.Trim(), i.UserId));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 1)]
    public async Task GetAllAsync_CombinesUserIdAndCompletionFilter(bool isCompleted, int expectedCount)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(isCompleted, "alice");

        Assert.Equal(expectedCount, items.Count);
        Assert.All(items, i => Assert.Equal(isCompleted, i.IsCompleted));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllAsync_NullOrWhitespaceUserId_ReturnsAllUsers(string? userId)
    {
        var repository = await CreateRepositoryAsync();

        Assert.Equal(4, (await repository.GetAllAsync(userId: userId)).Count);
    }

    // --- Kontroler ----------------------------------------------------------------

    [Fact]
    public async Task Controller_GetAll_PassesUserIdToRepository_AndReturnsFilteredDtos()
    {
        var items = new[] { CreateItem("alice"), CreateItem("alice") };
        var repository = new Mock<ITodoRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.GetAllAsync(false, "alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        var controller = new TodosController(repository.Object, TimeProvider.System, NullLogger<TodosController>.Instance);

        var result = await controller.GetAll(false, "alice", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(ok.Value);
        Assert.Equal(items.Select(i => i.Id), dtos.Select(d => d.Id));
        Assert.All(dtos, d => Assert.Equal("alice", d.UserId));
        repository.VerifyAll();
    }
}

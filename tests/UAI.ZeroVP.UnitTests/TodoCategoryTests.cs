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
/// Unit tests for the optional <c>Category</c> property on todo items:
/// cloning, JSON persistence, repository filtering, DTO validation, mapping, and controller pass-through.
/// </summary>
public sealed class TodoCategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static TodoItem CreateItem(
        string? category = null,
        bool isCompleted = false,
        string? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Task",
        IsCompleted = isCompleted,
        UserId = userId,
        Category = category,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now
    };

    // --- Entity & Persistence ---------------------------------------------------

    [Theory]
    [InlineData("Work")]
    [InlineData(null)]
    public void Clone_CopiesCategory(string? category)
    {
        Assert.Equal(category, CreateItem(category: category).Clone().Category);
    }

    [Fact]
    public async Task SaveToFileAsync_ThenLoad_PreservesCategory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"zerovp-category-test-{Guid.NewGuid():N}");
        var storagePath = Path.Combine(tempDirectory, "todos.json");
        try
        {
            var categorized = CreateItem(category: "Work");
            var uncategorized = CreateItem(category: null);

            var repository = new InMemoryTodoRepository(new InMemoryTodoOptions
            {
                SeedSampleData = false,
                StorageFilePath = storagePath
            });
            await repository.AddAsync(categorized);
            await repository.AddAsync(uncategorized);
            await repository.SaveToFileAsync();

            var reloaded = new InMemoryTodoRepository(new InMemoryTodoOptions
            {
                SeedSampleData = false,
                StorageFilePath = storagePath
            });

            var loadedCategorized = await reloaded.GetByIdAsync(categorized.Id);
            var loadedUncategorized = await reloaded.GetByIdAsync(uncategorized.Id);

            Assert.NotNull(loadedCategorized);
            Assert.Equal("Work", loadedCategorized.Category);
            Assert.NotNull(loadedUncategorized);
            Assert.Null(loadedUncategorized.Category);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadFromFile_LegacyJsonWithoutCategory_DefaultsCategoryToNull()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"zerovp-legacy-test-{Guid.NewGuid():N}");
        var storagePath = Path.Combine(tempDirectory, "todos.json");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
            const string legacyJson = """
                [
                  {
                    "id": "11111111-1111-1111-1111-111111111111",
                    "title": "Legacy task",
                    "description": "Created before categories existed",
                    "priority": "Normal",
                    "isCompleted": false,
                    "createdAtUtc": "2026-10-01T08:00:00Z",
                    "completedAtUtc": null,
                    "dueAtUtc": null,
                    "userId": "alice",
                    "updatedAtUtc": "2026-10-01T08:00:00Z"
                  }
                ]
                """;
            await File.WriteAllTextAsync(storagePath, legacyJson);

            var repository = new InMemoryTodoRepository(new InMemoryTodoOptions
            {
                SeedSampleData = false,
                StorageFilePath = storagePath
            });

            var loaded = await repository.GetByIdAsync(id);
            Assert.NotNull(loaded);
            Assert.Equal("Legacy task", loaded.Title);
            Assert.Null(loaded.Category);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    // --- Repository filtering ---------------------------------------------------

    private static async Task<InMemoryTodoRepository> CreateRepositoryAsync()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = false });
        await repository.AddAsync(CreateItem(category: "Work", isCompleted: false, userId: "alice"));
        await repository.AddAsync(CreateItem(category: "work", isCompleted: true, userId: "alice"));
        await repository.AddAsync(CreateItem(category: "Personal", isCompleted: false, userId: "bob"));
        await repository.AddAsync(CreateItem(category: null, isCompleted: false, userId: "alice"));
        return repository;
    }

    [Theory]
    [InlineData("Work", 2)]
    [InlineData("WORK", 2)]
    [InlineData("  wOrK  ", 2)]
    [InlineData("personal", 1)]
    [InlineData("unknown", 0)]
    public async Task GetAllAsync_FiltersByCategory_CaseInsensitiveAndTrimmed(string category, int expectedCount)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(category: category);

        Assert.Equal(expectedCount, items.Count);
        Assert.All(items, item => Assert.Equal(category.Trim(), item.Category, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllAsync_NullOrWhitespaceCategory_ReturnsAllCategories(string? category)
    {
        var repository = await CreateRepositoryAsync();

        Assert.Equal(4, (await repository.GetAllAsync(category: category)).Count);
    }

    [Theory]
    [InlineData(false, "alice", "WORK", 1)]
    [InlineData(true, "alice", "work", 1)]
    [InlineData(false, "bob", "work", 0)]
    [InlineData(false, "bob", "personal", 1)]
    public async Task GetAllAsync_CombinesCategoryWithCompletionAndUserId(
        bool isCompleted,
        string userId,
        string category,
        int expectedCount)
    {
        var repository = await CreateRepositoryAsync();

        var items = await repository.GetAllAsync(isCompleted, userId, category);

        Assert.Equal(expectedCount, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(isCompleted, item.IsCompleted);
            Assert.Equal(userId, item.UserId);
            Assert.Equal(category.Trim(), item.Category, StringComparer.OrdinalIgnoreCase);
        });
    }

    // --- DTO validation ---------------------------------------------------------

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Requests_ValidateCategoryMaxLength(int length, bool expectedValid)
    {
        var category = new string('c', length);
        object[] requests =
        [
            new CreateTodoItemRequest { Title = "Task", Category = category },
            new UpdateTodoItemRequest { Title = "Task", Category = category },
            new RepeatTodoItemRequest { Title = "Task", FirstDueAtUtc = Now, RepeatCount = 1, Category = category }
        ];

        foreach (var request in requests)
        {
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true);
            Assert.Equal(expectedValid, isValid);
        }
    }

    // --- Mapping ----------------------------------------------------------------

    [Theory]
    [InlineData("Work", "Work")]
    [InlineData("  Work  ", "Work")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToEntity_TrimsCategory_OrStoresNull(string? input, string? expected)
    {
        var entity = new CreateTodoItemRequest { Title = "Task", Category = input }.ToEntity(Now);

        Assert.Equal(expected, entity.Category);
    }

    [Fact]
    public void ToEntities_AssignsTrimmedCategoryToAllOccurrences()
    {
        var request = new RepeatTodoItemRequest
        {
            Title = "Weekly report",
            FirstDueAtUtc = Now,
            RepeatCount = 3,
            Category = "  Work  "
        };

        var entities = request.ToEntities(Now);

        Assert.Equal(4, entities.Count);
        Assert.All(entities, e => Assert.Equal("Work", e.Category));
    }

    [Theory]
    [InlineData("  Personal  ", "Personal")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void ApplyTo_OverwritesCategory(string? input, string? expected)
    {
        var entity = CreateItem(category: "Work");

        new UpdateTodoItemRequest { Title = "Task", Category = input }.ApplyTo(entity, Now);

        Assert.Equal(expected, entity.Category);
    }

    [Theory]
    [InlineData("Work")]
    [InlineData(null)]
    public void ToDto_MapsCategory(string? category)
    {
        Assert.Equal(category, CreateItem(category: category).ToDto().Category);
    }

    // --- Controller -------------------------------------------------------------

    [Fact]
    public async Task Controller_GetAll_PassesCategoryToRepository_AndReturnsFilteredDtos()
    {
        var items = new[] { CreateItem(category: "Work"), CreateItem(category: "Work") };
        var repository = new Mock<ITodoRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.GetAllAsync(false, "alice", "Work", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        var controller = new TodosController(repository.Object, TimeProvider.System, NullLogger<TodosController>.Instance);

        var result = await controller.GetAll(false, "alice", "Work", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(ok.Value);
        Assert.Equal(items.Select(i => i.Id), dtos.Select(d => d.Id));
        Assert.All(dtos, d => Assert.Equal("Work", d.Category));
        repository.VerifyAll();
    }
}

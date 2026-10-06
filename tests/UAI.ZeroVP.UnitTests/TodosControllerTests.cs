using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Testy REST kontroleru úkolů v izolaci – repozitář se podvrhuje přes
/// <see cref="Mock{T}"/> rozhraní <see cref="ITodoRepository"/> a čas přes pevný
/// <see cref="TimeProvider"/>. Ověřuje se vrácený HTTP status, tělo odpovědi
/// i to, s jakými daty kontroler volá repozitář.
/// </summary>
public sealed class TodosControllerTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 30, 0, TimeSpan.Zero);

    private readonly Mock<ITodoRepository> _repository = new(MockBehavior.Strict);
    private readonly TodosController _controller;

    public TodosControllerTests()
    {
        _controller = new TodosController(
            _repository.Object,
            new FixedTimeProvider(Now),
            NullLogger<TodosController>.Instance);
    }

    private static TodoItem CreateEntity(
        string title = "Úkol",
        bool isCompleted = false,
        TodoPriority priority = TodoPriority.Normal) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Priority = priority,
        IsCompleted = isCompleted,
        CreatedAtUtc = CreatedAt,
        CompletedAtUtc = isCompleted ? CreatedAt : null,
        UpdatedAtUtc = CreatedAt
    };

    private static void AssertNotFound(IConvertToActionResult result) =>
        AssertNotFound(result.Convert());

    private static void AssertNotFound(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    // --- GetAll -----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAll_ReturnsOkWithMappedItems_AndPassesFilter(bool? isCompleted)
    {
        var items = new[] { CreateEntity("První"), CreateEntity("Druhý") };
        _repository
            .Setup(r => r.GetAllAsync(isCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _controller.GetAll(isCompleted, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(ok.Value);
        Assert.Equal(items.Select(i => i.Id), dtos.Select(d => d.Id));
        Assert.Equal(["První", "Druhý"], dtos.Select(d => d.Title));
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAll_EmptyRepository_ReturnsOkWithEmptyList()
    {
        _repository
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetAll(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(ok.Value));
    }

    // --- GetById ----------------------------------------------------------------

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        var entity = CreateEntity("Nalezený", priority: TodoPriority.High);
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _controller.GetById(entity.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var dto = Assert.IsType<TodoItemDto>(ok.Value);
        Assert.Equal(entity.Id, dto.Id);
        Assert.Equal("Nalezený", dto.Title);
        Assert.Equal(TodoPriority.High, dto.Priority);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItem?)null);

        var result = await _controller.GetById(id, CancellationToken.None);

        AssertNotFound(result);
    }

    // --- Create -----------------------------------------------------------------

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAtRouteWithDto()
    {
        TodoItem? stored = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()))
            .Callback<TodoItem, CancellationToken>((item, _) => stored = item)
            .ReturnsAsync((TodoItem item, CancellationToken _) => item);
        var request = new CreateTodoItemRequest
        {
            Title = "  Nový úkol  ",
            Description = "Popis",
            Priority = TodoPriority.High
        };

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(TodosController.GetById), created.RouteName);
        Assert.NotNull(stored);
        Assert.Equal(stored.Id, created.RouteValues!["id"]);

        var dto = Assert.IsType<TodoItemDto>(created.Value);
        Assert.Equal(stored.Id, dto.Id);
        Assert.Equal("Nový úkol", dto.Title);
        Assert.Equal("Popis", dto.Description);
        Assert.Equal(TodoPriority.High, dto.Priority);
        Assert.False(dto.IsCompleted);
        Assert.Equal(Now, dto.CreatedAtUtc);
        Assert.Null(dto.CompletedAtUtc);
    }

    [Fact]
    public async Task Create_ReturnsItemAsStoredByRepository()
    {
        var fromRepository = CreateEntity("Uloženo repozitářem");
        _repository
            .Setup(r => r.AddAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fromRepository);

        var result = await _controller.Create(new CreateTodoItemRequest { Title = "Vstup" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(fromRepository.Id, created.RouteValues!["id"]);
        Assert.Equal(fromRepository.Id, Assert.IsType<TodoItemDto>(created.Value).Id);
    }

    // --- Update -----------------------------------------------------------------

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedDto()
    {
        var entity = CreateEntity("Původní");
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repository
            .Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var request = new UpdateTodoItemRequest
        {
            Title = "Upraveno",
            Description = "Nový popis",
            Priority = TodoPriority.Low,
            IsCompleted = true
        };

        var result = await _controller.Update(entity.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var dto = Assert.IsType<TodoItemDto>(ok.Value);
        Assert.Equal(entity.Id, dto.Id);
        Assert.Equal("Upraveno", dto.Title);
        Assert.Equal("Nový popis", dto.Description);
        Assert.Equal(TodoPriority.Low, dto.Priority);
        Assert.True(dto.IsCompleted);
        Assert.Equal(Now, dto.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound_AndDoesNotUpdate()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItem?)null);

        var result = await _controller.Update(id, new UpdateTodoItemRequest { Title = "X" }, CancellationToken.None);

        AssertNotFound(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ItemDeletedMeanwhile_ReturnsNotFound()
    {
        var entity = CreateEntity();
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repository
            .Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Update(entity.Id, new UpdateTodoItemRequest { Title = "X" }, CancellationToken.None);

        AssertNotFound(result);
    }

    // --- Complete ---------------------------------------------------------------

    [Fact]
    public async Task Complete_OpenItem_ReturnsOkAndSetsCompletionTime()
    {
        var entity = CreateEntity();
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repository
            .Setup(r => r.UpdateAsync(It.Is<TodoItem>(i => i.Id == entity.Id && i.IsCompleted), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Complete(entity.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var dto = Assert.IsType<TodoItemDto>(ok.Value);
        Assert.True(dto.IsCompleted);
        Assert.Equal(Now, dto.CompletedAtUtc);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Complete_AlreadyCompletedItem_KeepsOriginalCompletionTime()
    {
        var entity = CreateEntity(isCompleted: true);
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repository
            .Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Complete(entity.Id, CancellationToken.None);

        var dto = Assert.IsType<TodoItemDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(dto.IsCompleted);
        Assert.Equal(CreatedAt, dto.CompletedAtUtc);
    }

    [Fact]
    public async Task Complete_UnknownId_ReturnsNotFound_AndDoesNotUpdate()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItem?)null);

        var result = await _controller.Complete(id, CancellationToken.None);

        AssertNotFound(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_ItemDeletedMeanwhile_ReturnsNotFound()
    {
        var entity = CreateEntity();
        _repository
            .Setup(r => r.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repository
            .Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Complete(entity.Id, CancellationToken.None);

        AssertNotFound(result);
    }

    // --- Delete -----------------------------------------------------------------

    [Fact]
    public async Task Delete_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Delete(id, CancellationToken.None);

        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Delete(id, CancellationToken.None);

        AssertNotFound(result);
    }

    // --- CancellationToken ------------------------------------------------------

    [Fact]
    public async Task GetById_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, cts.Token))
            .ReturnsAsync((TodoItem?)null);

        await _controller.GetById(id, cts.Token);

        _repository.VerifyAll();
    }

    /// <summary>Pevný čas, aby se daly ověřit časové značky nastavené kontrolerem.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

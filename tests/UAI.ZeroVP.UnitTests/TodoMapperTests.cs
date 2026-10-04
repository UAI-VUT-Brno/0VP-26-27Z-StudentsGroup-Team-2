using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Testy mapování mezi entitou a DTO – <see cref="TodoMapper"/> se testuje přímo
/// jako běžná statická třída. Pokrývá <c>ToDto</c>, <c>ToEntity</c>, <c>ApplyTo</c>
/// a přechody stavu dokončení v <c>SetCompletionState</c>.
/// </summary>
public sealed class TodoMapperTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 30, 0, TimeSpan.Zero);

    private static TodoItem CreateEntity(bool isCompleted = false, DateTimeOffset? completedAtUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Úkol",
        Description = "Popis",
        Priority = TodoPriority.Normal,
        IsCompleted = isCompleted,
        CreatedAtUtc = CreatedAt,
        CompletedAtUtc = completedAtUtc,
        UpdatedAtUtc = CreatedAt
    };

    // --- ToDto ------------------------------------------------------------------

    [Fact]
    public void ToDto_CopiesAllPublicFields()
    {
        var entity = CreateEntity(isCompleted: true, completedAtUtc: Now);
        entity.Priority = TodoPriority.High;

        var dto = entity.ToDto();

        Assert.Equal(entity.Id, dto.Id);
        Assert.Equal("Úkol", dto.Title);
        Assert.Equal("Popis", dto.Description);
        Assert.Equal(TodoPriority.High, dto.Priority);
        Assert.True(dto.IsCompleted);
        Assert.Equal(CreatedAt, dto.CreatedAtUtc);
        Assert.Equal(Now, dto.CompletedAtUtc);
    }

    [Fact]
    public void ToDto_DoesNotExposeUpdatedAtUtc()
    {
        Assert.Null(typeof(TodoItemDto).GetProperty(nameof(TodoItem.UpdatedAtUtc)));
    }

    // --- ToEntity ---------------------------------------------------------------

    [Fact]
    public void ToEntity_CreatesOpenItemWithNewIdAndTimestamps()
    {
        var request = new CreateTodoItemRequest
        {
            Title = "Nakoupit",
            Description = "Mléko",
            Priority = TodoPriority.Low
        };

        var entity = request.ToEntity(Now);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Nakoupit", entity.Title);
        Assert.Equal("Mléko", entity.Description);
        Assert.Equal(TodoPriority.Low, entity.Priority);
        Assert.False(entity.IsCompleted);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Equal(Now, entity.CreatedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void ToEntity_GeneratesUniqueIdForEachCall()
    {
        var request = new CreateTodoItemRequest { Title = "Úkol" };

        Assert.NotEqual(request.ToEntity(Now).Id, request.ToEntity(Now).Id);
    }

    [Fact]
    public void ToEntity_TrimsTitleAndDescription()
    {
        var request = new CreateTodoItemRequest { Title = "  Nakoupit  ", Description = "  Mléko  " };

        var entity = request.ToEntity(Now);

        Assert.Equal("Nakoupit", entity.Title);
        Assert.Equal("Mléko", entity.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEntity_BlankDescription_BecomesNull(string? description)
    {
        var request = new CreateTodoItemRequest { Title = "Úkol", Description = description };

        Assert.Null(request.ToEntity(Now).Description);
    }

    [Fact]
    public void ToEntity_DefaultPriority_IsNormal()
    {
        var request = new CreateTodoItemRequest { Title = "Úkol" };

        Assert.Equal(TodoPriority.Normal, request.ToEntity(Now).Priority);
    }

    // --- ApplyTo ----------------------------------------------------------------

    [Fact]
    public void ApplyTo_OverwritesFieldsAndKeepsIdentity()
    {
        var entity = CreateEntity();
        var originalId = entity.Id;
        var request = new UpdateTodoItemRequest
        {
            Title = "  Nový název  ",
            Description = "   ",
            Priority = TodoPriority.High,
            IsCompleted = true
        };

        request.ApplyTo(entity, Now);

        Assert.Equal(originalId, entity.Id);
        Assert.Equal(CreatedAt, entity.CreatedAtUtc);
        Assert.Equal("Nový název", entity.Title);
        Assert.Null(entity.Description);
        Assert.Equal(TodoPriority.High, entity.Priority);
        Assert.True(entity.IsCompleted);
        Assert.Equal(Now, entity.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }

    // --- SetCompletionState -----------------------------------------------------

    [Fact]
    public void SetCompletionState_OpenToCompleted_SetsCompletionTime()
    {
        var entity = CreateEntity();

        entity.SetCompletionState(isCompleted: true, Now);

        Assert.True(entity.IsCompleted);
        Assert.Equal(Now, entity.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void SetCompletionState_AlreadyCompleted_KeepsOriginalCompletionTime()
    {
        var entity = CreateEntity(isCompleted: true, completedAtUtc: CreatedAt);

        entity.SetCompletionState(isCompleted: true, Now);

        Assert.True(entity.IsCompleted);
        Assert.Equal(CreatedAt, entity.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void SetCompletionState_CompletedToOpen_ClearsCompletionTime()
    {
        var entity = CreateEntity(isCompleted: true, completedAtUtc: CreatedAt);

        entity.SetCompletionState(isCompleted: false, Now);

        Assert.False(entity.IsCompleted);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }

    [Fact]
    public void SetCompletionState_OpenToOpen_StaysWithoutCompletionTime()
    {
        var entity = CreateEntity();

        entity.SetCompletionState(isCompleted: false, Now);

        Assert.False(entity.IsCompleted);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Equal(Now, entity.UpdatedAtUtc);
    }
}

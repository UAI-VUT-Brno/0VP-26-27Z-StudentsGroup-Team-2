using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Testy paměťového úložiště úkolů – testuje se přímo <see cref="InMemoryTodoRepository"/>,
/// bez mockování. Pokrývá seedování ukázkových dat, CRUD operace a hraniční stavy
/// (neexistující Id, duplicity, filtrování a řazení).
/// </summary>
public sealed class InMemoryTodoRepositoryTests
{
    private static InMemoryTodoRepository CreateEmptyRepository() =>
        new(new InMemoryTodoOptions { SeedSampleData = false });

    private static TodoItem CreateItem(
        string title = "Testovací úkol",
        TodoPriority priority = TodoPriority.Normal,
        bool isCompleted = false,
        DateTimeOffset? createdAtUtc = null,
        string? description = null)
    {
        var created = createdAtUtc ?? DateTimeOffset.UtcNow;

        return new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            IsCompleted = isCompleted,
            CreatedAtUtc = created,
            CompletedAtUtc = isCompleted ? created : null,
            UpdatedAtUtc = created
        };
    }

    // --- Seedování ukázkových dat ---------------------------------------------

    [Fact]
    public async Task Constructor_WithSeedEnabled_InsertsFourSampleItems()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = true });

        Assert.Equal(4, await repository.CountAsync());
    }

    [Fact]
    public async Task Constructor_WithoutOptions_SeedsByDefault()
    {
        var repository = new InMemoryTodoRepository();

        Assert.Equal(4, await repository.CountAsync());
    }

    [Fact]
    public async Task Constructor_WithSeedDisabled_StartsEmpty()
    {
        var repository = CreateEmptyRepository();

        Assert.Equal(0, await repository.CountAsync());
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Seed_CompletedItemsHaveCompletionTime_OpenItemsDoNot()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = true });

        var items = await repository.GetAllAsync();

        Assert.All(items, item => Assert.Equal(item.IsCompleted, item.CompletedAtUtc is not null));
        Assert.All(items, item => Assert.True(item.CreatedAtUtc <= DateTimeOffset.UtcNow));
    }

    // --- Přidání a čtení --------------------------------------------------------

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsStoredItem()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(title: "Napsat testy", priority: TodoPriority.High, description: "xUnit");

        await repository.AddAsync(item);
        var loaded = await repository.GetByIdAsync(item.Id);

        Assert.NotNull(loaded);
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal("Napsat testy", loaded.Title);
        Assert.Equal("xUnit", loaded.Description);
        Assert.Equal(TodoPriority.High, loaded.Priority);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task AddAsync_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem();
        await repository.AddAsync(item);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(item.Clone()));
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task AddAsync_WithNull_ThrowsArgumentNullException()
    {
        var repository = CreateEmptyRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!));
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var repository = CreateEmptyRepository();

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    // --- Izolace uloženého stavu (Clone) -----------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsCopy_ChangesDoNotAffectStoredItem()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(title: "Původní");
        await repository.AddAsync(item);

        var loaded = await repository.GetByIdAsync(item.Id);
        loaded!.Title = "Změněno mimo repozitář";

        var reloaded = await repository.GetByIdAsync(item.Id);
        Assert.Equal("Původní", reloaded!.Title);
    }

    [Fact]
    public async Task AddAsync_StoresCopy_LaterChangesOfOriginalAreIgnored()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(title: "Původní");

        await repository.AddAsync(item);
        item.Title = "Změněno po uložení";

        var loaded = await repository.GetByIdAsync(item.Id);
        Assert.Equal("Původní", loaded!.Title);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCopies_ChangesDoNotAffectStoredItems()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(title: "Původní");
        await repository.AddAsync(item);

        var all = await repository.GetAllAsync();
        all[0].Title = "Změněno";

        Assert.Equal("Původní", (await repository.GetByIdAsync(item.Id))!.Title);
    }

    // --- Filtrování a řazení ------------------------------------------------------

    [Theory]
    [InlineData(null, 3)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task GetAllAsync_FiltersByCompletionState(bool? isCompleted, int expectedCount)
    {
        var repository = CreateEmptyRepository();
        await repository.AddAsync(CreateItem(title: "Hotový", isCompleted: true));
        await repository.AddAsync(CreateItem(title: "Otevřený 1"));
        await repository.AddAsync(CreateItem(title: "Otevřený 2"));

        var items = await repository.GetAllAsync(isCompleted);

        Assert.Equal(expectedCount, items.Count);
        if (isCompleted is not null)
        {
            Assert.All(items, item => Assert.Equal(isCompleted.Value, item.IsCompleted));
        }
    }

    [Fact]
    public async Task GetAllAsync_OrdersByPriorityDescending_ThenByCreationTime()
    {
        var repository = CreateEmptyRepository();
        var now = DateTimeOffset.UtcNow;

        // Vkládáme záměrně v "nesprávném" pořadí.
        await repository.AddAsync(CreateItem("Low", TodoPriority.Low, createdAtUtc: now.AddHours(-5)));
        await repository.AddAsync(CreateItem("High novější", TodoPriority.High, createdAtUtc: now.AddHours(-1)));
        await repository.AddAsync(CreateItem("Normal", TodoPriority.Normal, createdAtUtc: now.AddHours(-3)));
        await repository.AddAsync(CreateItem("High starší", TodoPriority.High, createdAtUtc: now.AddHours(-2)));

        var titles = (await repository.GetAllAsync()).Select(item => item.Title).ToList();

        Assert.Equal(new[] { "High starší", "High novější", "Normal", "Low" }, titles);
    }

    // --- Aktualizace ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ExistingItem_ReturnsTrueAndPersistsChanges()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(title: "Před");
        await repository.AddAsync(item);

        var toUpdate = (await repository.GetByIdAsync(item.Id))!;
        toUpdate.Title = "Po";
        toUpdate.Priority = TodoPriority.High;

        Assert.True(await repository.UpdateAsync(toUpdate));

        var reloaded = (await repository.GetByIdAsync(item.Id))!;
        Assert.Equal("Po", reloaded.Title);
        Assert.Equal(TodoPriority.High, reloaded.Priority);
    }

    [Fact]
    public async Task UpdateAsync_UnknownItem_ReturnsFalseAndDoesNotInsert()
    {
        var repository = CreateEmptyRepository();

        Assert.False(await repository.UpdateAsync(CreateItem()));
        Assert.Equal(0, await repository.CountAsync());
    }

    // --- Mazání ----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ExistingItem_ReturnsTrueAndRemovesIt()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem();
        await repository.AddAsync(item);

        Assert.True(await repository.DeleteAsync(item.Id));
        Assert.Null(await repository.GetByIdAsync(item.Id));
        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_UnknownItem_ReturnsFalse()
    {
        var repository = CreateEmptyRepository();

        Assert.False(await repository.DeleteAsync(Guid.NewGuid()));
    }

    // --- Zrušení operace přes CancellationToken -----------------------------------------

    [Fact]
    public async Task AllOperations_WithCancelledToken_ThrowOperationCanceledException()
    {
        var repository = CreateEmptyRepository();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var token = cts.Token;

        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.GetAllAsync(null, null, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.GetByIdAsync(Guid.NewGuid(), token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.AddAsync(CreateItem(), token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.UpdateAsync(CreateItem(), token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.DeleteAsync(Guid.NewGuid(), token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.CountAsync(token));
    }
}

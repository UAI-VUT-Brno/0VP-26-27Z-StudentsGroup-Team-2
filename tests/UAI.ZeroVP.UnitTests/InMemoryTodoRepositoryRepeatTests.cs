using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Tests for batch storing (<see cref="InMemoryTodoRepository.AddRangeAsync"/>) and the
/// <see cref="TodoItem.DueAtUtc"/> field – the data layer for weekly recurring tasks.
/// </summary>
public sealed class InMemoryTodoRepositoryRepeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstDue = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static InMemoryTodoRepository CreateEmptyRepository() =>
        new(new InMemoryTodoOptions { SeedSampleData = false });

    private static TodoItem CreateItem(DateTimeOffset? dueAtUtc, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Weekly task",
        Description = "Repeats every week",
        Priority = TodoPriority.High,
        IsCompleted = false,
        CreatedAtUtc = Now,
        CompletedAtUtc = null,
        DueAtUtc = dueAtUtc,
        UpdatedAtUtc = Now
    };

    private static List<TodoItem> CreateWeeklyItems(int count) =>
        Enumerable.Range(0, count).Select(n => CreateItem(FirstDue.AddDays(7 * n))).ToList();

    // --- DueAtUtc on the entity -------------------------------------------------

    [Fact]
    public void Clone_CopiesDueAtUtc()
    {
        var item = CreateItem(FirstDue);

        Assert.Equal(FirstDue, item.Clone().DueAtUtc);
    }

    [Fact]
    public async Task AddAsync_ItemWithoutDueDate_KeepsNull()
    {
        var repository = CreateEmptyRepository();
        var item = CreateItem(dueAtUtc: null);

        await repository.AddAsync(item);

        Assert.Null((await repository.GetByIdAsync(item.Id))!.DueAtUtc);
    }

    [Fact]
    public async Task Seed_SampleItemsHaveNoDueDate()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = true });

        Assert.All(await repository.GetAllAsync(), item => Assert.Null(item.DueAtUtc));
    }

    // --- AddRangeAsync – successful store -------------------------------------

    [Fact]
    public async Task AddRangeAsync_StoresAllItems_VisibleThroughGetByIdAndGetAll()
    {
        var repository = CreateEmptyRepository();
        var items = CreateWeeklyItems(4);

        await repository.AddRangeAsync(items);

        var all = await repository.GetAllAsync();
        Assert.Equal(4, all.Count);
        foreach (var item in items)
        {
            var stored = await repository.GetByIdAsync(item.Id);
            Assert.NotNull(stored);
            Assert.Equal(item.DueAtUtc, stored.DueAtUtc);
            Assert.Contains(all, a => a.Id == item.Id && a.DueAtUtc == item.DueAtUtc);
        }
    }

    [Fact]
    public async Task AddRangeAsync_ReturnsItemsInInputOrder()
    {
        var repository = CreateEmptyRepository();
        var items = CreateWeeklyItems(3);

        var result = await repository.AddRangeAsync(items);

        Assert.Equal(items.Select(i => i.Id), result.Select(r => r.Id));
        Assert.Equal(items.Select(i => i.DueAtUtc), result.Select(r => r.DueAtUtc));
    }

    [Fact]
    public async Task AddRangeAsync_IncreasesCountByItemCount()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = true });
        var before = await repository.CountAsync();

        await repository.AddRangeAsync(CreateWeeklyItems(5));

        Assert.Equal(before + 5, await repository.CountAsync());
    }

    [Fact]
    public async Task AddRangeAsync_EmptyCollection_StoresNothing()
    {
        var repository = CreateEmptyRepository();

        var result = await repository.AddRangeAsync([]);

        Assert.Empty(result);
        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task AddRangeAsync_ReturnedAndInputItems_AreIndependentCopies()
    {
        var repository = CreateEmptyRepository();
        var items = CreateWeeklyItems(2);

        var result = await repository.AddRangeAsync(items);
        items[0].Title = "Changed in input";
        items[0].DueAtUtc = null;
        result[1].Title = "Changed in result";

        var first = await repository.GetByIdAsync(items[0].Id);
        var second = await repository.GetByIdAsync(items[1].Id);
        Assert.Equal("Weekly task", first!.Title);
        Assert.Equal(FirstDue, first.DueAtUtc);
        Assert.Equal("Weekly task", second!.Title);
    }

    // --- AddRangeAsync – all-or-nothing ---------------------------------------

    [Fact]
    public async Task AddRangeAsync_DuplicateIdInBatch_ThrowsAndStoresNothing()
    {
        var repository = CreateEmptyRepository();
        var id = Guid.NewGuid();
        var items = new List<TodoItem> { CreateItem(FirstDue), CreateItem(FirstDue.AddDays(7), id), CreateItem(FirstDue.AddDays(14), id) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddRangeAsync(items));

        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task AddRangeAsync_IdAlreadyStored_ThrowsAndStoresNothingNew()
    {
        var repository = CreateEmptyRepository();
        var existing = CreateItem(dueAtUtc: null);
        await repository.AddAsync(existing);
        var items = new List<TodoItem> { CreateItem(FirstDue), CreateItem(FirstDue.AddDays(7)), CreateItem(FirstDue.AddDays(14), existing.Id) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddRangeAsync(items));

        Assert.Equal(1, await repository.CountAsync());
        Assert.Null(await repository.GetByIdAsync(items[0].Id));
        Assert.Null(await repository.GetByIdAsync(items[1].Id));
        Assert.Null((await repository.GetByIdAsync(existing.Id))!.DueAtUtc);
    }

    [Fact]
    public async Task AddRangeAsync_NullItemInCollection_ThrowsAndStoresNothing()
    {
        var repository = CreateEmptyRepository();
        var items = new List<TodoItem> { CreateItem(FirstDue), null! };

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddRangeAsync(items));

        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task AddRangeAsync_NullCollection_Throws()
    {
        var repository = CreateEmptyRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddRangeAsync(null!));
    }

    [Fact]
    public async Task AddRangeAsync_CancelledToken_ThrowsAndStoresNothing()
    {
        var repository = CreateEmptyRepository();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddRangeAsync(CreateWeeklyItems(3), cts.Token));

        Assert.Equal(0, await repository.CountAsync());
    }

    // --- Occurrences are independent ---------------------------------------------

    [Fact]
    public async Task DeleteAsync_OneOccurrence_OthersRemain()
    {
        var repository = CreateEmptyRepository();
        var items = CreateWeeklyItems(4);
        await repository.AddRangeAsync(items);

        await repository.DeleteAsync(items[1].Id);

        Assert.Equal(3, await repository.CountAsync());
        Assert.Null(await repository.GetByIdAsync(items[1].Id));
        Assert.NotNull(await repository.GetByIdAsync(items[0].Id));
        Assert.NotNull(await repository.GetByIdAsync(items[2].Id));
        Assert.NotNull(await repository.GetByIdAsync(items[3].Id));
    }

    [Fact]
    public async Task UpdateAsync_CompletingOneOccurrence_OthersStayIncomplete()
    {
        var repository = CreateEmptyRepository();
        var items = CreateWeeklyItems(3);
        await repository.AddRangeAsync(items);

        var first = (await repository.GetByIdAsync(items[0].Id))!;
        first.IsCompleted = true;
        first.CompletedAtUtc = Now.AddHours(1);
        await repository.UpdateAsync(first);

        Assert.True((await repository.GetByIdAsync(items[0].Id))!.IsCompleted);
        Assert.Equal(FirstDue, (await repository.GetByIdAsync(items[0].Id))!.DueAtUtc);
        Assert.False((await repository.GetByIdAsync(items[1].Id))!.IsCompleted);
        Assert.False((await repository.GetByIdAsync(items[2].Id))!.IsCompleted);
        Assert.Single(await repository.GetAllAsync(isCompleted: true));
    }
}

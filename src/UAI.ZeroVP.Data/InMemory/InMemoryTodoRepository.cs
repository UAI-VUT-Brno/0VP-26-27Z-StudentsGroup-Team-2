using System.Collections.Concurrent;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Data.InMemory;

/// <summary>
/// Úložiště úkolů v operační paměti procesu.
/// <para>
/// POZOR – klíčový bod pro výuku Dockeru: data žijí jen uvnitř běžícího procesu.
/// Zastavením kontejneru (nebo jen restartem aplikace) se nenávratně ztratí
/// a každá replika kontejneru má vlastní, na ostatních nezávislou sadu dat.
/// </para>
/// <para>
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> zajišťuje bezpečný souběžný přístup –
/// webový server obsluhuje požadavky ve více vláknech současně.
/// </para>
/// </summary>
public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, TodoItem> _items = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Vytvoří úložiště a volitelně ho naplní ukázkovými daty.
    /// </summary>
    /// <param name="options">Nastavení úložiště.</param>
    /// <param name="timeProvider">Zdroj času – umožňuje čas podvrhnout v testech.</param>
    public InMemoryTodoRepository(InMemoryTodoOptions? options = null, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (options?.SeedSampleData ?? true)
        {
            Seed();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(bool? isCompleted = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userFilter = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

        IReadOnlyList<TodoItem> result = _items.Values
            .Where(item => isCompleted is null || item.IsCompleted == isCompleted)
            .Where(item => userFilter is null || item.UserId?.Trim() == userFilter)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAtUtc)
            .Select(item => item.Clone())
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.TryGetValue(id, out var item) ? item.Clone() : null);
    }

    /// <inheritdoc />
    public Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = item.Clone();
        if (!_items.TryAdd(stored.Id, stored))
        {
            throw new InvalidOperationException($"Úkol s identifikátorem {stored.Id} již existuje.");
        }

        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> AddRangeAsync(IReadOnlyCollection<TodoItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        cancellationToken.ThrowIfCancellationRequested();

        if (items.Any(item => item is null))
        {
            throw new ArgumentNullException(nameof(items), "The collection contains a null task.");
        }

        var toStore = items.Select(item => item.Clone()).ToList();

        var duplicateId = toStore.GroupBy(item => item.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new InvalidOperationException($"Task with ID {duplicateId} is in the batch more than once.");
        }

        // Items are inserted one by one. If an Id already exists (e.g. another thread
        // added it in the meantime), everything this batch has added is removed again,
        // so only part of the repetitions is never stored.
        var added = new List<TodoItem>(toStore.Count);
        foreach (var stored in toStore)
        {
            if (!_items.TryAdd(stored.Id, stored))
            {
                foreach (var rollback in added)
                {
                    _items.TryRemove(rollback.Id, out _);
                }

                throw new InvalidOperationException($"Task with ID {stored.Id} already exists.");
            }

            added.Add(stored);
        }

        IReadOnlyList<TodoItem> result = added.Select(item => item.Clone()).ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        var replacement = item.Clone();

        // TryUpdate porovná uloženou instanci s tou, kterou jsme právě přečetli.
        // Když ji mezitím vyměnilo jiné vlákno, porovnání selže a čte se znovu.
        // Bez toho cyklu by souběžný zápis vrátil false, tedy 404 "úkol nenalezen",
        // přestože úkol existuje. Vyhrává poslední zapisující.
        while (_items.TryGetValue(item.Id, out var existing))
        {
            if (_items.TryUpdate(item.Id, replacement, existing))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_items.Count);
    }

    /// <summary>Vloží ukázkové úkoly, aby API hned po startu nevracelo prázdný seznam.</summary>
    private void Seed()
    {
        var now = _timeProvider.GetUtcNow();

        AddSeedItem("Připravit ukázkové API", "Tři projekty: DTO, Data, API.", TodoPriority.High, isCompleted: true, now.AddDays(-2));
        AddSeedItem("Napsat Dockerfile", "Vícefázový build nad SDK a runtime image.", TodoPriority.High, isCompleted: false, now.AddDays(-1));
        AddSeedItem("Vyzkoušet docker compose", "Spustit API ve dvou replikách.", TodoPriority.Normal, isCompleted: false, now.AddHours(-3));
        AddSeedItem("Uklidit nepoužité image", "docker image prune", TodoPriority.Low, isCompleted: false, now.AddMinutes(-30));
    }

    private void AddSeedItem(string title, string description, TodoPriority priority, bool isCompleted, DateTimeOffset createdAtUtc)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            IsCompleted = isCompleted,
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = isCompleted ? createdAtUtc.AddHours(1) : null,
            UpdatedAtUtc = createdAtUtc
        };

        _items[item.Id] = item;
    }
}

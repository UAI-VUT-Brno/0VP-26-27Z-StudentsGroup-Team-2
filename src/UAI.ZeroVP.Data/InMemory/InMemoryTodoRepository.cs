using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
/// With <see cref="InMemoryTodoOptions.StorageFilePath"/> set, the tasks survive a restart:
/// they are loaded from the JSON file when the repository is created and saved back
/// by <see cref="TodoStorageHostedService"/> when the application stops.
/// </para>
/// <para>
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> zajišťuje bezpečný souběžný přístup –
/// webový server obsluhuje požadavky ve více vláknech současně.
/// </para>
/// </summary>
public sealed class InMemoryTodoRepository : ITodoRepository
{
    // Same conventions as the API (camelCase, enums as strings), so the file is readable by hand.
    // Relaxed escaping keeps Czech diacritics as they are; the file is never embedded in HTML.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConcurrentDictionary<Guid, TodoItem> _items = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryTodoRepository> _logger;
    private readonly string? _storageFilePath;

    /// <summary>
    /// Vytvoří úložiště a volitelně ho naplní ukázkovými daty.
    /// If <see cref="InMemoryTodoOptions.StorageFilePath"/> points to a valid JSON file,
    /// the tasks are loaded from it instead and no sample data is added.
    /// </summary>
    /// <param name="options">Nastavení úložiště.</param>
    /// <param name="timeProvider">Zdroj času – umožňuje čas podvrhnout v testech.</param>
    /// <param name="logger">Logger for loading and saving the JSON file.</param>
    public InMemoryTodoRepository(
        InMemoryTodoOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<InMemoryTodoRepository>? logger = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<InMemoryTodoRepository>.Instance;

        if (!string.IsNullOrWhiteSpace(options?.StorageFilePath))
        {
            _storageFilePath = Path.GetFullPath(options.StorageFilePath);

            if (TryLoadFromFile(_storageFilePath))
            {
                return;
            }
        }

        if (options?.SeedSampleData ?? true)
        {
            Seed();
        }
    }

    /// <summary>
    /// Saves all tasks to <see cref="InMemoryTodoOptions.StorageFilePath"/>, creating the directory if needed.
    /// Does nothing when no storage file is configured.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveToFileAsync(CancellationToken cancellationToken = default)
    {
        if (_storageFilePath is null)
        {
            return;
        }

        var items = _items.Values
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => item.Clone())
            .ToList();

        var directory = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temporary file first and then replace the target, so an interrupted
        // write never leaves a half-written (corrupted) todos.json behind. The temporary name is
        // unique, so instances sharing one file (docker compose down stops them together) do not collide.
        var tempFilePath = $"{_storageFilePath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
        }

        File.Move(tempFilePath, _storageFilePath, overwrite: true);

        _logger.LogInformation("Saved {Count} tasks to {StorageFilePath}.", items.Count, _storageFilePath);
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

    /// <summary>
    /// Loads the tasks from the JSON file. Returns <c>false</c> when the file does not exist
    /// or is corrupted – the caller then starts from scratch.
    /// </summary>
    private bool TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogInformation("Storage file {StorageFilePath} does not exist, starting from scratch.", path);
            return false;
        }

        List<TodoItem?>? items;
        try
        {
            using var stream = File.OpenRead(path);
            items = JsonSerializer.Deserialize<List<TodoItem?>>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Storage file {StorageFilePath} is corrupted, starting from scratch.", path);
            return false;
        }

        if (items is null || items.Any(item => item is null))
        {
            _logger.LogError("Storage file {StorageFilePath} does not contain a list of tasks, starting from scratch.", path);
            return false;
        }

        foreach (var item in items)
        {
            _items[item!.Id] = item;
        }

        _logger.LogInformation("Loaded {Count} tasks from {StorageFilePath}.", _items.Count, path);
        return true;
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

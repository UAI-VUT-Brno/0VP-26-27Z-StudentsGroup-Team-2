using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.DependencyInjection;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.Data.InMemory;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Tests for the JSON file persistence of <see cref="InMemoryTodoRepository"/>
/// (<see cref="InMemoryTodoOptions.StorageFilePath"/>) and for saving on shutdown
/// through <see cref="TodoStorageHostedService"/>.
/// Every test works in its own temporary directory, which is deleted afterwards.
/// </summary>
public sealed class TodoJsonPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "UAI.ZeroVP.Tests", Guid.NewGuid().ToString("N"));
    private readonly Mock<ILogger<InMemoryTodoRepository>> _loggerMock = new();

    private string StorageFilePath => Path.Combine(_directory, "data", "todos.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private InMemoryTodoRepository CreateRepository(bool seedSampleData = false) => new(
        new InMemoryTodoOptions { SeedSampleData = seedSampleData, StorageFilePath = StorageFilePath },
        logger: _loggerMock.Object);

    private void WriteStorageFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorageFilePath)!);
        File.WriteAllText(StorageFilePath, content);
    }

    private static TodoItem CreateItem(string title, bool isCompleted = false, DateTimeOffset? dueAtUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = $"{title} description",
        Priority = TodoPriority.High,
        IsCompleted = isCompleted,
        CreatedAtUtc = Now,
        CompletedAtUtc = isCompleted ? Now.AddHours(1) : null,
        DueAtUtc = dueAtUtc,
        UpdatedAtUtc = Now.AddHours(2)
    };

    private void VerifyLogged(LogLevel level, Times times) =>
        _loggerMock.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    // --- Configuration --------------------------------------------------------

    [Fact]
    public void Options_StorageFilePath_IsNullByDefault()
    {
        Assert.Null(new InMemoryTodoOptions().StorageFilePath);
    }

    [Fact]
    public async Task SaveToFileAsync_WithoutStorageFilePath_DoesNothing()
    {
        var repository = new InMemoryTodoRepository(new InMemoryTodoOptions { SeedSampleData = true });

        await repository.SaveToFileAsync();

        Assert.False(Directory.Exists(_directory));
    }

    // --- Save and load --------------------------------------------------------

    [Fact]
    public async Task SaveToFileAsync_ThenNewInstance_LoadsAllTasksWithAllFields()
    {
        var first = CreateRepository();
        var open = CreateItem("Open task", dueAtUtc: Now.AddDays(7));
        var done = CreateItem("Done task", isCompleted: true);
        await first.AddAsync(open);
        await first.AddAsync(done);

        await first.SaveToFileAsync();
        var second = CreateRepository();

        Assert.Equal(2, await second.CountAsync());
        foreach (var expected in new[] { open, done })
        {
            var loaded = await second.GetByIdAsync(expected.Id);
            Assert.NotNull(loaded);
            Assert.Equal(expected.Title, loaded.Title);
            Assert.Equal(expected.Description, loaded.Description);
            Assert.Equal(expected.Priority, loaded.Priority);
            Assert.Equal(expected.IsCompleted, loaded.IsCompleted);
            Assert.Equal(expected.CreatedAtUtc, loaded.CreatedAtUtc);
            Assert.Equal(expected.CompletedAtUtc, loaded.CompletedAtUtc);
            Assert.Equal(expected.DueAtUtc, loaded.DueAtUtc);
            Assert.Equal(expected.UpdatedAtUtc, loaded.UpdatedAtUtc);
        }
    }

    [Fact]
    public async Task SaveToFileAsync_KeepsChangesAndDeletions()
    {
        var first = CreateRepository(seedSampleData: true);
        var all = await first.GetAllAsync();
        var changed = all[0];
        changed.Title = "Changed title";
        await first.UpdateAsync(changed);
        await first.DeleteAsync(all[1].Id);

        await first.SaveToFileAsync();
        var second = CreateRepository(seedSampleData: true);

        Assert.Equal(all.Count - 1, await second.CountAsync());
        Assert.Equal("Changed title", (await second.GetByIdAsync(changed.Id))!.Title);
        Assert.Null(await second.GetByIdAsync(all[1].Id));
    }

    [Fact]
    public async Task Constructor_ValidFile_LoadsOnlyTasksFromFile_WithoutSeeding()
    {
        var first = CreateRepository();
        await first.AddAsync(CreateItem("Only task"));
        await first.SaveToFileAsync();

        var second = CreateRepository(seedSampleData: true);

        var loaded = Assert.Single(await second.GetAllAsync());
        Assert.Equal("Only task", loaded.Title);
    }

    [Fact]
    public async Task Constructor_FileWithEmptyList_StartsEmpty_WithoutSeeding()
    {
        await CreateRepository().SaveToFileAsync();

        var repository = CreateRepository(seedSampleData: true);

        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task SaveToFileAsync_CreatesMissingDirectory_AndLeavesNoTemporaryFile()
    {
        var repository = CreateRepository(seedSampleData: true);

        await repository.SaveToFileAsync();

        Assert.True(File.Exists(StorageFilePath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(StorageFilePath)!, "*.tmp"));
    }

    [Fact]
    public async Task SaveToFileAsync_TwoInstancesSavingAtOnce_LeaveValidFile()
    {
        var first = CreateRepository(seedSampleData: true);
        var second = CreateRepository(seedSampleData: true);

        await Task.WhenAll(
            Task.Run(() => first.SaveToFileAsync()),
            Task.Run(() => second.SaveToFileAsync()));

        Assert.Equal(4, await CreateRepository().CountAsync());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(StorageFilePath)!, "*.tmp"));
    }

    [Fact]
    public async Task SaveToFileAsync_WritesReadableJson_WithEnumsAsStrings()
    {
        var repository = CreateRepository();
        await repository.AddAsync(CreateItem("Readable"));

        await repository.SaveToFileAsync();

        using var document = JsonDocument.Parse(File.ReadAllText(StorageFilePath));
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("Readable", item.GetProperty("title").GetString());
        Assert.Equal("High", item.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task SaveToFileAsync_OverwritesPreviousFile()
    {
        var repository = CreateRepository();
        await repository.AddAsync(CreateItem("First"));
        await repository.SaveToFileAsync();
        await repository.AddAsync(CreateItem("Second"));

        await repository.SaveToFileAsync();

        Assert.Equal(2, await CreateRepository().CountAsync());
    }

    // --- Missing file ---------------------------------------------------------

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 0)]
    public async Task Constructor_MissingFile_StartsFromScratch(bool seedSampleData, int expectedCount)
    {
        var repository = CreateRepository(seedSampleData);

        Assert.Equal(expectedCount, await repository.CountAsync());
        Assert.False(File.Exists(StorageFilePath));
        VerifyLogged(LogLevel.Error, Times.Never());
    }

    // --- Corrupted file -------------------------------------------------------

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("{\"title\": \"an object instead of a list\"}")]
    [InlineData("[{\"title\": \"missing required id and createdAtUtc\"}]")]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task Constructor_CorruptedFile_LogsError_AndStartsFromScratch(string content)
    {
        WriteStorageFile(content);

        var repository = CreateRepository();

        Assert.Equal(0, await repository.CountAsync());
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task Constructor_CorruptedFile_SeedsSampleDataWhenEnabled()
    {
        WriteStorageFile("{ this is not json");

        var repository = CreateRepository(seedSampleData: true);

        Assert.Equal(4, await repository.CountAsync());
    }

    [Fact]
    public async Task SaveToFileAsync_AfterCorruptedFile_ReplacesItWithValidJson()
    {
        WriteStorageFile("{ this is not json");
        var repository = CreateRepository(seedSampleData: true);

        await repository.SaveToFileAsync();

        Assert.Equal(4, await CreateRepository().CountAsync());
    }

    // --- Application lifetime (integration) -----------------------------------

    private IHost BuildHost(bool seedSampleData = false)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInMemoryTodoData(options =>
        {
            options.SeedSampleData = seedSampleData;
            options.StorageFilePath = StorageFilePath;
        });
        return builder.Build();
    }

    [Fact]
    public async Task Host_SavesTasksOnStop_AndNextHostLoadsThem()
    {
        var task = CreateItem("Survives restart", dueAtUtc: Now.AddDays(7));

        using (var host = BuildHost(seedSampleData: true))
        {
            await host.StartAsync();
            await host.Services.GetRequiredService<ITodoRepository>().AddAsync(task);
            await host.StopAsync();
        }

        Assert.True(File.Exists(StorageFilePath));

        using (var host = BuildHost(seedSampleData: true))
        {
            await host.StartAsync();
            var repository = host.Services.GetRequiredService<ITodoRepository>();

            Assert.Equal(5, await repository.CountAsync());
            Assert.Equal(task.DueAtUtc, (await repository.GetByIdAsync(task.Id))!.DueAtUtc);

            await host.StopAsync();
        }
    }

    [Fact]
    public void AddInMemoryTodoData_ResolvesSameRepositoryForInterfaceAndHostedService()
    {
        using var host = BuildHost();

        Assert.Same(
            host.Services.GetRequiredService<InMemoryTodoRepository>(),
            host.Services.GetRequiredService<ITodoRepository>());
        Assert.Contains(host.Services.GetServices<IHostedService>(), service => service is TodoStorageHostedService);
    }
}

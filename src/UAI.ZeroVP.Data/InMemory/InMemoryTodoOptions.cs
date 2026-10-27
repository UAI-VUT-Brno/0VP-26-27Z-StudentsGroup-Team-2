namespace UAI.ZeroVP.Data.InMemory;

/// <summary>
/// Nastavení paměťového úložiště.
/// V kontejneru se dá přepsat proměnnou prostředí, např. <c>Todo__SeedSampleData=false</c>.
/// </summary>
public sealed class InMemoryTodoOptions
{
    /// <summary>Název sekce v <c>appsettings.json</c>.</summary>
    public const string SectionName = "Todo";

    /// <summary>Naplnit úložiště ukázkovými úkoly při startu aplikace.</summary>
    public bool SeedSampleData { get; set; } = true;

    /// <summary>
    /// Path to the JSON file the tasks are loaded from on startup and saved to on shutdown
    /// (e.g. <c>Todo__StorageFilePath=/app/data/todos.json</c>). A relative path is resolved
    /// against the current working directory. <c>null</c> keeps the repository purely in memory.
    /// </summary>
    public string? StorageFilePath { get; set; }
}

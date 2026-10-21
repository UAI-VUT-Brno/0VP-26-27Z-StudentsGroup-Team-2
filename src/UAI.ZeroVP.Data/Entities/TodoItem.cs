using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.Data.Entities;

/// <summary>
/// Entita úkolu tak, jak ji drží datová vrstva.
/// Oproti <see cref="TodoItemDto"/> obsahuje i interní pole <see cref="UpdatedAtUtc"/>,
/// které se ven z API nikdy neposílá.
/// </summary>
public sealed class TodoItem
{
    /// <summary>Jednoznačný identifikátor úkolu.</summary>
    public required Guid Id { get; init; }

    /// <summary>Krátký název úkolu.</summary>
    public required string Title { get; set; }

    /// <summary>Volitelný podrobnější popis.</summary>
    public string? Description { get; set; }

    /// <summary>Priorita úkolu.</summary>
    public TodoPriority Priority { get; set; } = TodoPriority.Normal;

    /// <summary>Příznak, zda je úkol hotový.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Okamžik vytvoření (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Okamžik dokončení (UTC), nebo <c>null</c> pokud úkol hotový není.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Due date (UTC), or <c>null</c> if the task has no due date.</summary>
    public DateTimeOffset? DueAtUtc { get; set; }

    /// <summary>Interní audit – poslední změna záznamu. Do DTO se nepromítá.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Vytvoří hlubokou kopii entity. In-memory repozitář ji používá proto,
    /// aby volající nemohl omylem změnit uložený stav mimo repozitář
    /// (u databázového úložiště tuto roli plní mapování na řádky tabulky).
    /// </summary>
    public TodoItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
        Priority = Priority,
        IsCompleted = IsCompleted,
        CreatedAtUtc = CreatedAtUtc,
        CompletedAtUtc = CompletedAtUtc,
        DueAtUtc = DueAtUtc,
        UpdatedAtUtc = UpdatedAtUtc
    };
}

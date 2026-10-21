namespace UAI.ZeroVP.DTO;

/// <summary>
/// Úkol tak, jak ho API vrací klientovi.
/// Záměrně neobsahuje interní sloupce entity (např. <c>UpdatedAtUtc</c>) –
/// to je hlavní důvod, proč DTO existuje jako samostatná vrstva.
/// </summary>
public sealed record TodoItemDto
{
    /// <summary>Jednoznačný identifikátor úkolu.</summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public required Guid Id { get; init; }

    /// <summary>Krátký název úkolu.</summary>
    /// <example>Dokončit semestrální projekt</example>
    public required string Title { get; init; }

    /// <summary>Volitelný podrobnější popis.</summary>
    /// <example>Doplnit OpenAPI dokumentaci a otestovat ve Swagger UI.</example>
    public string? Description { get; init; }

    /// <summary>Priorita úkolu.</summary>
    /// <example>Normal</example>
    public TodoPriority Priority { get; init; }

    /// <summary>Příznak, zda je úkol hotový.</summary>
    /// <example>false</example>
    public bool IsCompleted { get; init; }

    /// <summary>Okamžik vytvoření (UTC).</summary>
    /// <example>2026-10-12T12:00:00Z</example>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Okamžik dokončení (UTC), nebo <c>null</c> pokud úkol hotový není.</summary>
    /// <example>null</example>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Termín splnění (UTC), nebo <c>null</c> pokud úkol termín nemá.</summary>
    /// <example>2026-10-19T09:00:00Z</example>
    public DateTimeOffset? DueAtUtc { get; init; }
}

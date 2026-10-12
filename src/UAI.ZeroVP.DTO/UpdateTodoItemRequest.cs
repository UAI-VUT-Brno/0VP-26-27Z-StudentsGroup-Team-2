using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Vstup pro úplnou aktualizaci úkolu (HTTP PUT /api/todos/{id}).
/// Jde o "full update" – neposlané hodnoty se přepíší výchozími.
/// </summary>
public sealed record UpdateTodoItemRequest
{
    /// <summary>
    /// Krátký název úkolu. Povinný, 1–200 znaků.
    /// <para>
    /// Bez klíčového slova <c>required</c> ze stejného důvodu jako
    /// u <see cref="CreateTodoItemRequest.Title"/>.
    /// </para>
    /// </summary>
    /// <example>Dokončit semestrální projekt</example>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Název úkolu je povinný.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Název musí mít 1 až 200 znaků.")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Volitelný popis, maximálně 1000 znaků.</summary>
    /// <example>Doplnit OpenAPI dokumentaci a otestovat ve Swagger UI.</example>
    [StringLength(1000, ErrorMessage = "Popis smí mít nejvýše 1000 znaků.")]
    public string? Description { get; init; }

    /// <summary>Priorita úkolu.</summary>
    /// <example>High</example>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Neplatná hodnota priority.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;

    /// <summary>Příznak dokončení úkolu.</summary>
    /// <example>true</example>
    public bool IsCompleted { get; init; }
}

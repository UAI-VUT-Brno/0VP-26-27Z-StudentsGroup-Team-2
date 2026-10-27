using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Vstup pro vytvoření nového úkolu (HTTP POST /api/todos).
/// Validační atributy vyhodnocuje ASP.NET Core automaticky díky <c>[ApiController]</c>.
/// </summary>
public sealed record CreateTodoItemRequest
{
    /// <summary>
    /// Krátký název úkolu. Povinný, 1–200 znaků.
    /// <para>
    /// Záměrně bez klíčového slova <c>required</c>: to vynucuje už deserializace JSON,
    /// takže úplně chybějící pole by skončilo chybou deserializace a hláška
    /// z <c>[Required]</c> by se ke klientovi nikdy nedostala.
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

    /// <summary>Priorita úkolu. Výchozí hodnota je <see cref="TodoPriority.Normal"/>.</summary>
    /// <example>Normal</example>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Neplatná hodnota priority.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;

    /// <summary>Volitelný identifikátor uživatele, kterému úkol patří, maximálně 200 znaků.</summary>
    /// <example>jan.novak</example>
    [StringLength(200, ErrorMessage = "Identifikátor uživatele smí mít nejvýše 200 znaků.")]
    public string? UserId { get; init; }
}

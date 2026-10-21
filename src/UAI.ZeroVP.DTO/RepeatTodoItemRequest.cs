using System.ComponentModel.DataAnnotations;

namespace UAI.ZeroVP.DTO;

/// <summary>
/// Vstup pro vytvoření týdenní řady úkolů (HTTP POST /api/todos/repeat).
/// Vznikne první výskyt a <see cref="RepeatCount"/> dalších, každý o 7 dní později.
/// Validační atributy vyhodnocuje ASP.NET Core automaticky díky <c>[ApiController]</c>.
/// </summary>
public sealed record RepeatTodoItemRequest
{
    /// <summary>Krátký název úkolu. Povinný, 1–200 znaků.</summary>
    /// <example>Odevzdat týdenní report</example>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Název úkolu je povinný.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Název musí mít 1 až 200 znaků.")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Volitelný popis, maximálně 1000 znaků.</summary>
    /// <example>Shrnout, co tým za týden udělal.</example>
    [StringLength(1000, ErrorMessage = "Popis smí mít nejvýše 1000 znaků.")]
    public string? Description { get; init; }

    /// <summary>Priorita úkolu. Výchozí hodnota je <see cref="TodoPriority.Normal"/>.</summary>
    /// <example>Normal</example>
    [EnumDataType(typeof(TodoPriority), ErrorMessage = "Neplatná hodnota priority.")]
    public TodoPriority Priority { get; init; } = TodoPriority.Normal;

    /// <summary>
    /// Termín prvního výskytu (UTC). Povinný – nullable typ je zde proto,
    /// aby chybějící hodnota skončila standardní validační chybou 400.
    /// </summary>
    /// <example>2026-10-19T09:00:00Z</example>
    [Required(ErrorMessage = "Termín prvního výskytu je povinný.")]
    public DateTimeOffset? FirstDueAtUtc { get; init; }

    /// <summary>Počet dalších týdenních opakování po prvním výskytu, 1–52.</summary>
    /// <example>3</example>
    [Range(1, 52, ErrorMessage = "Počet opakování musí být 1 až 52.")]
    public int RepeatCount { get; init; }
}

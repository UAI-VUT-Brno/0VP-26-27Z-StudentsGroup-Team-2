using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Mapping;

/// <summary>
/// Převody mezi entitou datové vrstvy a přenosovými objekty API.
/// Mapování patří do API vrstvy – ta jediná zná obě strany.
/// </summary>
public static class TodoMapper
{
    /// <summary>Převede entitu na výstupní DTO (interní pole se zahodí).</summary>
    public static TodoItemDto ToDto(this TodoItem entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Description = entity.Description,
        Priority = entity.Priority,
        IsCompleted = entity.IsCompleted,
        CreatedAtUtc = entity.CreatedAtUtc,
        CompletedAtUtc = entity.CompletedAtUtc,
        DueAtUtc = entity.DueAtUtc
    };

    /// <summary>Vytvoří novou entitu z požadavku na založení úkolu.</summary>
    /// <param name="request">Data z těla požadavku.</param>
    /// <param name="nowUtc">Aktuální čas – předává se zvenčí kvůli testovatelnosti.</param>
    public static TodoItem ToEntity(this CreateTodoItemRequest request, DateTimeOffset nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        Priority = request.Priority,
        IsCompleted = false,
        CreatedAtUtc = nowUtc,
        CompletedAtUtc = null,
        UpdatedAtUtc = nowUtc
    };

    /// <summary>
    /// Vytvoří týdenní řadu úkolů: první výskyt a <c>RepeatCount</c> dalších,
    /// každý o přesně 7 dní později. Výskyty jsou samostatné, navzájem nezávislé úkoly.
    /// </summary>
    /// <param name="request">Validovaná data z těla požadavku.</param>
    /// <param name="nowUtc">Aktuální čas – předává se zvenčí kvůli testovatelnosti.</param>
    public static IReadOnlyList<TodoItem> ToEntities(this RepeatTodoItemRequest request, DateTimeOffset nowUtc)
    {
        var firstDueAtUtc = (request.FirstDueAtUtc
            ?? throw new ArgumentException("FirstDueAtUtc je povinné.", nameof(request))).ToUniversalTime();
        var title = request.Title.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        return Enumerable.Range(0, request.RepeatCount + 1)
            .Select(n => new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
                Priority = request.Priority,
                IsCompleted = false,
                CreatedAtUtc = nowUtc,
                CompletedAtUtc = null,
                DueAtUtc = firstDueAtUtc.AddDays(7 * n),
                UpdatedAtUtc = nowUtc
            })
            .ToList();
    }

    /// <summary>
    /// Promítne data z požadavku do existující entity.
    /// Hlídá i přechod stavu dokončení, aby <c>CompletedAtUtc</c> odpovídal příznaku.
    /// </summary>
    public static void ApplyTo(this UpdateTodoItemRequest request, TodoItem entity, DateTimeOffset nowUtc)
    {
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Priority = request.Priority;
        entity.SetCompletionState(request.IsCompleted, nowUtc);
        entity.UpdatedAtUtc = nowUtc;
    }

    /// <summary>Nastaví příznak dokončení a dopočítá čas dokončení.</summary>
    public static void SetCompletionState(this TodoItem entity, bool isCompleted, DateTimeOffset nowUtc)
    {
        if (isCompleted && !entity.IsCompleted)
        {
            entity.CompletedAtUtc = nowUtc;
        }
        else if (!isCompleted)
        {
            entity.CompletedAtUtc = null;
        }

        entity.IsCompleted = isCompleted;
        entity.UpdatedAtUtc = nowUtc;
    }
}

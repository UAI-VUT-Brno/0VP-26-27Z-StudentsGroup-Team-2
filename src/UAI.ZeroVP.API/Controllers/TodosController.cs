using Microsoft.AspNetCore.Mvc;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.API.Controllers;

/// <summary>
/// REST rozhraní nad seznamem úkolů.
/// Kontroler zná pouze rozhraní <see cref="ITodoRepository"/> – nikoli to,
/// že za ním stojí paměťové úložiště.
/// </summary>
[ApiController]
[Route("api/todos")]
[Produces("application/json")]
public sealed class TodosController : ControllerBase
{
    private readonly ITodoRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TodosController> _logger;

    /// <summary>Vytvoří kontroler se závislostmi z DI kontejneru.</summary>
    public TodosController(ITodoRepository repository, TimeProvider timeProvider, ILogger<TodosController> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Vrátí seznam úkolů, volitelně filtrovaný podle stavu dokončení.</summary>
    /// <param name="isCompleted">Filtr <c>true</c>/<c>false</c>; vynechání znamená bez filtru.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="200">Seznam úkolů seřazený podle priority a data vzniku.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TodoItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> GetAll(
        [FromQuery] bool? isCompleted,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(isCompleted, cancellationToken);

        return Ok(items.Select(item => item.ToDto()).ToList());
    }

    /// <summary>Vrátí jeden úkol podle identifikátoru.</summary>
    /// <param name="id">Jednoznačný identifikátor úkolu.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="200">Nalezený úkol.</response>
    /// <response code="404">Úkol se zadaným identifikátorem neexistuje.</response>
    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);

        return item is null ? NotFoundTodo(id) : Ok(item.ToDto());
    }

    /// <summary>Založí nový úkol.</summary>
    /// <param name="request">Data pro vytvoření nového úkolu.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="201">Úkol byl vytvořen; hlavička <c>Location</c> odkazuje na nový zdroj.</response>
    /// <response code="400">Vstupní data neprošla validací.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoItemDto>> Create(
        [FromBody] CreateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        var entity = request.ToEntity(_timeProvider.GetUtcNow());
        var created = await _repository.AddAsync(entity, cancellationToken);

        _logger.LogInformation("Vytvořen úkol {TodoId} ({Title}).", created.Id, created.Title);

        return CreatedAtRoute(nameof(GetById), new { id = created.Id }, created.ToDto());
    }

    /// <summary>Vytvoří týdenní řadu opakujících se úkolů.</summary>
    /// <param name="request">Data pro vytvoření série úkolů včetně prvního termínu a počtu opakování.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="201">Řada úkolů byla vytvořena; hlavička <c>Location</c> odkazuje na seznam úkolů.</response>
    /// <response code="400">Vstupní data neprošla validací.</response>
    [HttpPost("repeat")]
    [ProducesResponseType(typeof(IReadOnlyList<TodoItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TodoItemDto>>> Repeat(
        [FromBody] RepeatTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        var entities = request.ToEntities(_timeProvider.GetUtcNow());
        var created = await _repository.AddRangeAsync(entities, cancellationToken);

        _logger.LogInformation("Vytvořeno {Count} opakovaných úkolů.", created.Count);

        var dtos = created.Select(item => item.ToDto()).ToList();
        return CreatedAtAction(nameof(GetAll), dtos);
    }

    /// <summary>Přepíše existující úkol novými hodnotami.</summary>
    /// <param name="id">Jednoznačný identifikátor úkolu.</param>
    /// <param name="request">Nová data pro úplnou aktualizaci úkolu.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="200">Aktualizovaný úkol.</response>
    /// <response code="400">Vstupní data neprošla validací.</response>
    /// <response code="404">Úkol se zadaným identifikátorem neexistuje.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> Update(
        Guid id,
        [FromBody] UpdateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFoundTodo(id);
        }

        request.ApplyTo(entity, _timeProvider.GetUtcNow());

        return await _repository.UpdateAsync(entity, cancellationToken)
            ? Ok(entity.ToDto())
            : NotFoundTodo(id);
    }

    /// <summary>Označí úkol za dokončený.</summary>
    /// <param name="id">Jednoznačný identifikátor úkolu.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="200">Úkol po změně stavu.</response>
    /// <response code="404">Úkol se zadaným identifikátorem neexistuje.</response>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoItemDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFoundTodo(id);
        }

        entity.SetCompletionState(isCompleted: true, _timeProvider.GetUtcNow());

        return await _repository.UpdateAsync(entity, cancellationToken)
            ? Ok(entity.ToDto())
            : NotFoundTodo(id);
    }

    /// <summary>Smaže úkol.</summary>
    /// <param name="id">Jednoznačný identifikátor úkolu.</param>
    /// <param name="cancellationToken">Token pro zrušení operace.</param>
    /// <response code="204">Úkol byl smazán.</response>
    /// <response code="404">Úkol se zadaným identifikátorem neexistuje.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await _repository.DeleteAsync(id, cancellationToken))
        {
            return NotFoundTodo(id);
        }

        _logger.LogInformation("Smazán úkol {TodoId}.", id);

        return NoContent();
    }

    /// <summary>Jednotná odpověď 404 ve formátu ProblemDetails.</summary>
    private ObjectResult NotFoundTodo(Guid id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Úkol nenalezen",
        detail: $"Úkol s identifikátorem {id} neexistuje.");
}
